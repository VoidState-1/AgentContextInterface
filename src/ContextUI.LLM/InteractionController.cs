using ContextUI.Core.Abstractions;
using ContextUI.Core.Models;
using ContextUI.Core.Services;
using ContextUI.Framework.Runtime;
using ContextUI.LLM.Abstractions;
using ContextUI.LLM.Services;
using System.Text.Json;

namespace ContextUI.LLM;

/// <summary>
/// 交互控制器 - 协调 LLM 调用和操作执行
/// </summary>
public class InteractionController
{
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

        // 2. 渲染上下文为 LLM 消息
        var activeItems = _contextManager.GetActive();
        var messages = _renderer.Render(activeItems, _windowManager, _renderOptions);

        // 3. 调用 LLM
        var llmResponse = await _llm.SendAsync(messages, ct);

        if (!llmResponse.Success)
        {
            return InteractionResult.Fail(llmResponse.Error ?? "LLM 调用失败");
        }

        var responseContent = llmResponse.Content ?? "";

        // 4. 添加 AI 响应到上下文
        _contextManager.Add(new ContextItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = ContextItemType.Assistant,
            Content = responseContent
        });

        // 5. 解析并执行操作
        var action = ActionParser.Parse(responseContent);
        ActionResult? actionResult = null;

        if (action != null)
        {
            actionResult = await ExecuteActionAsync(action);
        }

        _contextManager.Prune();

        return InteractionResult.Ok(responseContent, action, actionResult, llmResponse.Usage);
    }

    /// <summary>
    /// 执行解析后的操作
    /// </summary>
    private async Task<ActionResult> ExecuteActionAsync(ParsedAction action)
    {
        if (action.Type == "create")
        {
            return await ExecuteCreateAsync(action);
        }
        else if (action.Type == "action")
        {
            return await ExecuteWindowActionAsync(action);
        }

        return ActionResult.Fail($"未知操作类型: {action.Type}");
    }

    /// <summary>
    /// 执行 create 操作
    /// </summary>
    private Task<ActionResult> ExecuteCreateAsync(ParsedAction action)
    {
        try
        {
            var appName = action.AppName;

            // 如果没有指定应用名，打开应用启动器
            if (string.IsNullOrEmpty(appName))
            {
                appName = "launcher";
            }

            _host.Launch(appName, action.Target);

            return Task.FromResult(ActionResult.Ok($"已打开应用: {appName}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ActionResult.Fail($"打开应用失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 执行窗口操作
    /// </summary>
    private async Task<ActionResult> ExecuteWindowActionAsync(ParsedAction action)
    {
        if (string.IsNullOrEmpty(action.WindowId) || string.IsNullOrEmpty(action.ActionId))
        {
            return ActionResult.Fail("操作缺少必要参数");
        }

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
            _contextManager.Prune();
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

            _contextManager.Prune();
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

    /// <summary>
    /// 确保已初始化
    /// </summary>
    private void EnsureInitialized()
    {
        if (_initialized) return;

        // 添加系统提示词
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

    public static InteractionResult Ok(string response, ParsedAction? action = null, ActionResult? actionResult = null, TokenUsage? usage = null) =>
        new() { Success = true, Response = response, Action = action, ActionResult = actionResult, Usage = usage };

    public static InteractionResult Fail(string error) =>
        new() { Success = false, Error = error };
}

