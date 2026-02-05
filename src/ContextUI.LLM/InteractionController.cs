using ContextUI.Core.Abstractions;
using ContextUI.Core.Models;
using ContextUI.Core.Services;
using ContextUI.Framework.Runtime;
using ContextUI.LLM.Abstractions;
using ContextUI.LLM.Services;

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
    private readonly IContextRenderer _renderer;
    private readonly RenderOptions _renderOptions;

    private bool _initialized;

    public InteractionController(
        ILLMBridge llm,
        FrameworkHost host,
        IContextManager contextManager,
        IWindowManager windowManager,
        IContextRenderer? renderer = null,
        RenderOptions? renderOptions = null)
    {
        _llm = llm;
        _host = host;
        _contextManager = contextManager;
        _windowManager = windowManager;
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
                appName = "app_launcher";
            }

            var window = _host.Launch(appName, action.Target);

            // 添加窗口到上下文
            _contextManager.Add(new ContextItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = ContextItemType.Window,
                Content = window.Id
            });

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

        var window = _windowManager.Get(action.WindowId);
        if (window == null)
        {
            return ActionResult.Fail($"窗口不存在: {action.WindowId}");
        }

        // 处理关闭操作
        if (action.ActionId == "close")
        {
            _windowManager.Remove(action.WindowId);
            _contextManager.MarkWindowObsolete(action.WindowId);
            return ActionResult.Close(action.Parameters?.GetValueOrDefault("summary")?.ToString());
        }

        // 执行窗口操作
        if (window.Handler == null)
        {
            return ActionResult.Fail("窗口不支持操作");
        }

        var context = new ActionContext
        {
            Window = window,
            ActionId = action.ActionId,
            Parameters = action.Parameters
        };

        var result = await window.Handler.ExecuteAsync(context);

        // 如果操作结果要求刷新窗口
        if (result.ShouldRefresh)
        {
            _host.RefreshWindow(action.WindowId);
        }

        // 如果操作结果要求关闭窗口
        if (result.ShouldClose)
        {
            _windowManager.Remove(action.WindowId);
            _contextManager.MarkWindowObsolete(action.WindowId);
        }

        return result;
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

