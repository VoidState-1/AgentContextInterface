using ACI.Core.Abstractions;
using ACI.Core.Models;
using ACI.Core.Services;
using ACI.Framework.Runtime;
using ACI.LLM.Abstractions;
using ACI.LLM.Services;
using System.Text;
using System.Text.Json;

namespace ACI.LLM;

/// <summary>
/// 交互控制器 - 协调 LLM 调用和操作执行
/// </summary>
public class InteractionController
{
    private const int MaxAutoToolCallTurns = 12;

    private readonly ILLMBridge _llm;
    private readonly FrameworkHost _host;
    private readonly IContextManager _contextManager;
    private readonly IWindowManager _windowManager;
    private readonly ActionExecutor _actionExecutor;
    private readonly IContextRenderer _renderer;
    private readonly RenderOptions _renderOptions;

    private bool _initialized;

    public InteractionController(
        ILLMBridge llm,
        FrameworkHost host,
        IContextManager contextManager,
        IWindowManager windowManager,
        ActionExecutor actionExecutor,
        IContextRenderer? renderer = null,
        RenderOptions? renderOptions = null)
    {
        _llm = llm;
        _host = host;
        _contextManager = contextManager;
        _windowManager = windowManager;
        _actionExecutor = actionExecutor;
        _renderer = renderer ?? new ContextRenderer();
        _renderOptions = renderOptions ?? new RenderOptions();
    }

    /// <summary>
    /// 处理用户请求
    /// </summary>
    public async Task<InteractionResult> ProcessAsync(string userMessage, CancellationToken ct = default)
    {
        // 确保已初始化（添加系统提示词）
        EnsureInitialized();

        // 1. 添加用户消息到上下文
        _contextManager.Add(new ContextItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = ContextItemType.User,
            Content = userMessage
        });

        ParsedAction? lastAction = null;
        ActionResult? lastActionResult = null;
        string lastResponseContent = "";
        var totalUsage = new TokenUsage();
        var steps = new List<InteractionStep>();

        // Auto-loop: execute tool_call responses until the model returns
        // a normal response without tool_call.
        for (var turn = 0; turn <= MaxAutoToolCallTurns; turn++)
        {
            PruneContext();
            var activeItems = _contextManager.GetActive();
            var messages = _renderer.Render(activeItems, _windowManager, _renderOptions);

            var llmResponse = await _llm.SendAsync(messages, ct);
            if (!llmResponse.Success)
            {
                return InteractionResult.Fail(llmResponse.Error ?? "LLM 调用失败");
            }

            AccumulateUsage(totalUsage, llmResponse.Usage);

            lastResponseContent = llmResponse.Content ?? "";
            _contextManager.Add(new ContextItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = ContextItemType.Assistant,
                Content = lastResponseContent
            });

            var parsedActionBatch = ActionParser.Parse(lastResponseContent);
            if (parsedActionBatch == null)
            {
                PruneContext();
                return InteractionResult.Ok(lastResponseContent, lastAction, lastActionResult, totalUsage, steps);
            }

            for (var i = 0; i < parsedActionBatch.Calls.Count; i++)
            {
                var parsedAction = parsedActionBatch.Calls[i];
                var mode = ResolveActionMode(parsedAction.WindowId, parsedAction.ActionId);
                var callId = $"call_{turn + 1}_{i + 1}";
                var actionResult = await ExecuteActionAsync(parsedAction);

                lastAction = parsedAction;
                lastActionResult = actionResult;

                steps.Add(new InteractionStep
                {
                    CallId = callId,
                    WindowId = parsedAction.WindowId,
                    ActionId = parsedAction.ActionId,
                    ResolvedMode = mode == ActionExecutionMode.Async ? "async" : "sync",
                    Success = actionResult.Success,
                    Message = actionResult.Message,
                    Summary = actionResult.Summary,
                    TaskId = TryExtractTaskId(actionResult.Data),
                    Turn = turn + 1,
                    Index = i + 1
                });
            }
        }

        PruneContext();
        return InteractionResult.Fail($"已经连续执行 {MaxAutoToolCallTurns + 1} 次 tool_call，仍未返回非 tool_call 响应");
    }

    /// <summary>
    /// 调试入口：直接处理 AI 输出（不调用 LLM）
    /// </summary>
    public async Task<InteractionResult> ProcessAssistantOutputAsync(
        string assistantOutput,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        EnsureInitialized();

        _contextManager.Add(new ContextItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = ContextItemType.Assistant,
            Content = assistantOutput
        });

        var parsedActionBatch = ActionParser.Parse(assistantOutput);
        ParsedAction? lastAction = null;
        ActionResult? lastActionResult = null;
        var steps = new List<InteractionStep>();
        if (parsedActionBatch != null)
        {
            for (var i = 0; i < parsedActionBatch.Calls.Count; i++)
            {
                var action = parsedActionBatch.Calls[i];
                var mode = ResolveActionMode(action.WindowId, action.ActionId);
                var callId = $"call_1_{i + 1}";
                var actionResult = await ExecuteActionAsync(action);

                lastAction = action;
                lastActionResult = actionResult;

                steps.Add(new InteractionStep
                {
                    CallId = callId,
                    WindowId = action.WindowId,
                    ActionId = action.ActionId,
                    ResolvedMode = mode == ActionExecutionMode.Async ? "async" : "sync",
                    Success = actionResult.Success,
                    Message = actionResult.Message,
                    Summary = actionResult.Summary,
                    TaskId = TryExtractTaskId(actionResult.Data),
                    Turn = 1,
                    Index = i + 1
                });
            }
        }

        PruneContext();

        return InteractionResult.Ok(assistantOutput, lastAction, lastActionResult, steps: steps);
    }

    /// <summary>
    /// 调试入口：获取当前将发送给 LLM 的消息快照
    /// </summary>
    public IReadOnlyList<LlmMessage> GetCurrentLlmInputSnapshot()
    {
        EnsureInitialized();
        PruneContext();
        var activeItems = _contextManager.GetActive();
        return _renderer.Render(activeItems, _windowManager, _renderOptions);
    }

    /// <summary>
    /// 调试入口：将当前 LLM 输入渲染为单块文本
    /// </summary>
    public string GetCurrentLlmInputRaw()
    {
        var messages = GetCurrentLlmInputSnapshot();

        var sb = new StringBuilder();
        foreach (var msg in messages)
        {
            sb.AppendLine($"[{msg.Role}]");
            sb.AppendLine(msg.Content);
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// 执行解析后的操作
    /// </summary>
    private async Task<ActionResult> ExecuteActionAsync(ParsedAction action)
    {
        return await ExecuteWindowActionAsync(action);
    }

    /// <summary>
    /// 执行窗口操作
    /// </summary>
    private async Task<ActionResult> ExecuteWindowActionAsync(ParsedAction action)
    {
        return await ExecuteWindowActionAsync(action.WindowId, action.ActionId, action.Parameters);
    }

    /// <summary>
    /// 执行窗口操作（供 API 端点复用）
    /// </summary>
    public async Task<ActionResult> ExecuteWindowActionAsync(
        string windowId,
        string actionId,
        Dictionary<string, object>? parameters = null)
    {
        if (string.IsNullOrWhiteSpace(windowId) || string.IsNullOrWhiteSpace(actionId))
        {
            return ActionResult.Fail("操作缺少必要参数");
        }

        var result = await _actionExecutor.ExecuteAsync(windowId, actionId, parameters);

        if (!result.Success)
        {
            return result;
        }

        if (!TryExtractLaunchCommand(result.Data, out var appName, out var target, out var closeSource))
        {
            PruneContext();
            return result;
        }

        if (string.IsNullOrWhiteSpace(appName))
        {
            return ActionResult.Fail("启动命令缺少应用名称");
        }

        try
        {
            _host.Launch(appName, target);

            if (closeSource)
            {
                await _actionExecutor.ExecuteAsync(
                    windowId,
                    "close",
                    new Dictionary<string, object>
                    {
                        ["summary"] = $"已从窗口 {windowId} 打开应用 {appName}"
                    });
            }

            PruneContext();
            return result;
        }
        catch (Exception ex)
        {
            return ActionResult.Fail($"打开应用失败: {ex.Message}");
        }
    }

    private static bool TryExtractLaunchCommand(
        object? data,
        out string? appName,
        out string? target,
        out bool closeSource)
    {
        appName = null;
        target = null;
        closeSource = false;

        if (data == null)
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(data));
            var root = doc.RootElement;

            if (!root.TryGetProperty("action", out var actionElement) ||
                actionElement.ValueKind != JsonValueKind.String ||
                !string.Equals(actionElement.GetString(), "launch", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (root.TryGetProperty("app", out var appElement) && appElement.ValueKind == JsonValueKind.String)
            {
                appName = appElement.GetString();
            }

            if (root.TryGetProperty("target", out var targetElement) && targetElement.ValueKind == JsonValueKind.String)
            {
                target = targetElement.GetString();
            }

            if (root.TryGetProperty("close_source", out var closeElement) &&
                (closeElement.ValueKind == JsonValueKind.True || closeElement.ValueKind == JsonValueKind.False))
            {
                closeSource = closeElement.GetBoolean();
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? TryExtractTaskId(object? data)
    {
        if (data == null)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(data));
            var root = doc.RootElement;
            if (!root.TryGetProperty("task_id", out var taskIdElement) ||
                taskIdElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return taskIdElement.GetString();
        }
        catch
        {
            return null;
        }
    }

    private ActionExecutionMode ResolveActionMode(string windowId, string actionId)
    {
        if (string.Equals(actionId, "close", StringComparison.OrdinalIgnoreCase))
        {
            return ActionExecutionMode.Sync;
        }

        var window = _windowManager.Get(windowId);
        var action = window?.Actions.FirstOrDefault(a => string.Equals(a.Id, actionId, StringComparison.Ordinal));
        return action?.Mode ?? ActionExecutionMode.Sync;
    }

    private static void AccumulateUsage(TokenUsage total, TokenUsage? delta)
    {
        if (delta == null)
        {
            return;
        }

        total.PromptTokens += delta.PromptTokens;
        total.CompletionTokens += delta.CompletionTokens;
        total.TotalTokens += delta.TotalTokens;
    }

    private void PruneContext()
    {
        _contextManager.Prune(
            _windowManager,
            _renderOptions.MaxTokens,
            _renderOptions.MinConversationTokens,
            _renderOptions.TrimToTokens);
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;

        _contextManager.Add(new ContextItem
        {
            Id = "system_prompt",
            Type = ContextItemType.System,
            Content = PromptBuilder.BuildSystemPrompt()
        });

        _initialized = true;
    }
}

/// <summary>
/// 交互结果
/// </summary>
public class InteractionResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public string? Response { get; init; }
    public ParsedAction? Action { get; init; }
    public ActionResult? ActionResult { get; init; }
    public TokenUsage? Usage { get; init; }
    public IReadOnlyList<InteractionStep>? Steps { get; init; }

    public static InteractionResult Ok(
        string response,
        ParsedAction? action = null,
        ActionResult? actionResult = null,
        TokenUsage? usage = null,
        IReadOnlyList<InteractionStep>? steps = null) =>
        new()
        {
            Success = true,
            Response = response,
            Action = action,
            ActionResult = actionResult,
            Usage = usage,
            Steps = steps
        };

    public static InteractionResult Fail(string error) =>
        new() { Success = false, Error = error };
}

/// <summary>
/// 单次交互内的调用执行步骤
/// </summary>
public class InteractionStep
{
    public required string CallId { get; init; }
    public required string WindowId { get; init; }
    public required string ActionId { get; init; }
    public required string ResolvedMode { get; init; }
    public required bool Success { get; init; }
    public string? Message { get; init; }
    public string? Summary { get; init; }
    public string? TaskId { get; init; }
    public int Turn { get; init; }
    public int Index { get; init; }
}
