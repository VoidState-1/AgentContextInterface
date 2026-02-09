using ACI.Core.Abstractions;
using ACI.Core.Models;
using ACI.Core.Services;
using ACI.Framework.Runtime;
using ACI.LLM.Abstractions;
using System.Text;
using System.Text.Json;

namespace ACI.LLM;

/// <summary>
/// 交互控制器外观，循环编排由 `InteractionOrchestrator` 负责。
/// </summary>
public class InteractionController
{
    /// <summary>
    /// 自动工具调用最大轮次。
    /// </summary>
    private const int MaxAutoToolCallTurns = 12;

    /// <summary>
    /// 核心依赖服务。
    /// </summary>
    private readonly FrameworkHost _host;
    private readonly IContextManager _contextManager;
    private readonly IWindowManager _windowManager;
    private readonly ActionExecutor _actionExecutor;
    private readonly Func<string, Func<CancellationToken, Task>, string?, string>? _startBackgroundTask;

    /// <summary>
    /// 渲染与编排组件。
    /// </summary>
    private readonly IContextRenderer _renderer;
    private readonly RenderOptions _renderOptions;
    private readonly InteractionOrchestrator _orchestrator;

    /// <summary>
    /// 内部状态。
    /// </summary>
    private bool _initialized;

    /// <summary>
    /// 创建交互控制器。
    /// </summary>
    public InteractionController(
        ILLMBridge llm,
        FrameworkHost host,
        IContextManager contextManager,
        IWindowManager windowManager,
        ActionExecutor actionExecutor,
        IContextRenderer? renderer = null,
        RenderOptions? renderOptions = null,
        Func<string, Func<CancellationToken, Task>, string?, string>? startBackgroundTask = null)
    {
        _host = host;
        _contextManager = contextManager;
        _windowManager = windowManager;
        _actionExecutor = actionExecutor;
        _startBackgroundTask = startBackgroundTask;
        _renderer = renderer ?? new ContextRenderer();
        _renderOptions = renderOptions ?? new RenderOptions();

        _orchestrator = new InteractionOrchestrator(
            llm,
            _contextManager,
            _windowManager,
            _renderer,
            _renderOptions,
            EnsureInitialized,
            ExecuteToolCallAsync,
            MaxAutoToolCallTurns);
    }

    /// <summary>
    /// 处理一条用户消息。
    /// </summary>
    public Task<InteractionResult> ProcessAsync(string userMessage, CancellationToken ct = default)
        => _orchestrator.ProcessUserMessageAsync(userMessage, ct);

    /// <summary>
    /// 处理 assistant 输出文本。
    /// </summary>
    public Task<InteractionResult> ProcessAssistantOutputAsync(string assistantOutput, CancellationToken ct = default)
        => _orchestrator.ProcessAssistantOutputAsync(assistantOutput, ct);

    /// <summary>
    /// 获取当前发送给 LLM 的消息快照。
    /// </summary>
    public IReadOnlyList<LlmMessage> GetCurrentLlmInputSnapshot()
    {
        EnsureInitialized();
        PruneContext();
        var activeItems = _contextManager.GetActive();
        return _renderer.Render(activeItems, _windowManager, _renderOptions);
    }

    /// <summary>
    /// 获取当前 LLM 输入的纯文本格式。
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
    /// 执行窗口动作（允许异步分发）。
    /// </summary>
    public async Task<ActionResult> ExecuteWindowActionAsync(
        string windowId,
        string actionId,
        JsonElement? parameters = null)
    {
        return await ExecuteWindowActionInternalAsync(
            windowId,
            actionId,
            parameters,
            allowAsyncDispatch: true,
            CancellationToken.None);
    }

    /// <summary>
    /// 执行窗口动作内部流程。
    /// </summary>
    private async Task<ActionResult> ExecuteWindowActionInternalAsync(
        string windowId,
        string actionId,
        JsonElement? parameters,
        bool allowAsyncDispatch,
        CancellationToken ct)
    {
        // 1. 校验参数并判断是否进入异步分发分支。
        if (string.IsNullOrWhiteSpace(windowId) || string.IsNullOrWhiteSpace(actionId))
        {
            return ActionResult.Fail("action is missing required fields");
        }

        if (allowAsyncDispatch &&
            _startBackgroundTask != null &&
            ResolveActionMode(windowId, actionId) == ActionExecutionMode.Async)
        {
            var taskId = _startBackgroundTask(
                windowId,
                async token =>
                {
                    await ExecuteWindowActionInternalAsync(
                        windowId,
                        actionId,
                        parameters,
                        allowAsyncDispatch: false,
                        token);
                },
                null);

            return ActionResult.Ok(
                message: $"async task started: {taskId}",
                summary: $"start async action {windowId}.{actionId}",
                shouldRefresh: false,
                data: new
                {
                    task_id = taskId,
                    status = "running"
                });
        }

        // 2. 执行动作处理器并处理失败返回。
        ct.ThrowIfCancellationRequested();

        var result = await _actionExecutor.ExecuteAsync(windowId, actionId, parameters);
        if (!result.Success)
        {
            return result;
        }

        // 3. 仅当返回 launch 指令时追加应用启动流程。
        if (!TryExtractLaunchCommand(result.Data, out var appName, out var target, out var closeSource))
        {
            PruneContext();
            return result;
        }

        if (string.IsNullOrWhiteSpace(appName))
        {
            return ActionResult.Fail("launch command missing app name");
        }

        // 4. 执行启动并按需关闭来源窗口，最后裁剪上下文。
        try
        {
            _host.Launch(appName, target);

            if (closeSource)
            {
                ct.ThrowIfCancellationRequested();
                await _actionExecutor.ExecuteAsync(
                    windowId,
                    "close",
                    JsonSerializer.SerializeToElement(new
                    {
                        summary = $"Opened app {appName} from window {windowId}"
                    }));
            }

            PruneContext();
            return result;
        }
        catch (Exception ex)
        {
            return ActionResult.Fail($"launch failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 执行一次工具调用并生成步骤记录。
    /// </summary>
    private async Task<ToolCallExecution> ExecuteToolCallAsync(
        ParsedAction action,
        int turn,
        int index,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var resolvedMode = ResolveActionMode(action.WindowId, action.ActionId);
        var callId = $"call_{turn}_{index}";
        var actionResult = await ExecuteWindowActionAsync(action.WindowId, action.ActionId, action.Parameters);

        var step = new InteractionStep
        {
            CallId = callId,
            WindowId = action.WindowId,
            ActionId = action.ActionId,
            ResolvedMode = resolvedMode == ActionExecutionMode.Async ? "async" : "sync",
            Success = actionResult.Success,
            Message = actionResult.Message,
            Summary = actionResult.Summary,
            TaskId = TryExtractTaskId(actionResult.Data),
            Turn = turn,
            Index = index
        };

        return new ToolCallExecution
        {
            Action = action,
            Result = actionResult,
            Step = step
        };
    }

    /// <summary>
    /// 解析动作最终执行模式。
    /// </summary>
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

    /// <summary>
    /// 触发上下文裁剪。
    /// </summary>
    private void PruneContext()
    {
        _contextManager.Prune(
            _windowManager,
            _renderOptions.MaxTokens,
            _renderOptions.MinConversationTokens,
            _renderOptions.PruneTargetTokens);
    }

    /// <summary>
    /// 注入系统提示词（仅首次）。
    /// </summary>
    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        _contextManager.Add(new ContextItem
        {
            Id = "system_prompt",
            Type = ContextItemType.System,
            Content = PromptBuilder.BuildSystemPrompt()
        });

        _initialized = true;
    }

    /// <summary>
    /// 从动作返回数据中解析 launch 指令。
    /// </summary>
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

    /// <summary>
    /// 从动作返回数据中提取后台任务 ID。
    /// </summary>
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
}

/// <summary>
/// 一次交互请求的返回结果。
/// </summary>
public class InteractionResult
{
    /// <summary>
    /// 是否处理成功。
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 失败原因。
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// assistant 的最终文本响应。
    /// </summary>
    public string? Response { get; init; }

    /// <summary>
    /// 最后一个解析到的动作。
    /// </summary>
    public ParsedAction? Action { get; init; }

    /// <summary>
    /// 最后一个动作执行结果。
    /// </summary>
    public ActionResult? ActionResult { get; init; }

    /// <summary>
    /// 本次交互累计 Token 使用量。
    /// </summary>
    public TokenUsage? Usage { get; init; }

    /// <summary>
    /// 工具调用步骤列表。
    /// </summary>
    public IReadOnlyList<InteractionStep>? Steps { get; init; }

    /// <summary>
    /// 创建成功结果。
    /// </summary>
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

    /// <summary>
    /// 创建失败结果。
    /// </summary>
    public static InteractionResult Fail(string error)
        => new() { Success = false, Error = error };
}

/// <summary>
/// 单个工具调用的执行步骤记录。
/// </summary>
public class InteractionStep
{
    /// <summary>
    /// 调用 ID。
    /// </summary>
    public required string CallId { get; init; }

    /// <summary>
    /// 目标窗口 ID。
    /// </summary>
    public required string WindowId { get; init; }

    /// <summary>
    /// 动作 ID。
    /// </summary>
    public required string ActionId { get; init; }

    /// <summary>
    /// 解析后的执行模式（sync/async）。
    /// </summary>
    public required string ResolvedMode { get; init; }

    /// <summary>
    /// 是否执行成功。
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// 执行结果消息。
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// 执行摘要。
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// 后台任务 ID（异步动作时存在）。
    /// </summary>
    public string? TaskId { get; init; }

    /// <summary>
    /// 所在回合序号（从 1 开始）。
    /// </summary>
    public int Turn { get; init; }

    /// <summary>
    /// 回合内调用序号（从 1 开始）。
    /// </summary>
    public int Index { get; init; }
}
