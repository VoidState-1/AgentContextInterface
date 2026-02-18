using ACI.Core.Abstractions;
using ACI.Core.Models;
using System.Text.Json;

namespace ACI.Core.Services;

/// <summary>
/// 执行窗口 Action 并发布执行事件。
/// </summary>
public class ActionExecutor : IActionExecutor
{
    /// <summary>
    /// 核心依赖。
    /// </summary>
    private readonly IWindowManager _windows;
    private readonly ISeqClock _clock;
    private readonly IEventBus _events;
    private readonly ReservedActionDispatcher _reservedActionDispatcher;

    /// <summary>
    /// 可选的窗口刷新回调。
    /// </summary>
    private readonly Action<string>? _refreshWindow;

    /// <summary>
    /// 创建执行器。
    /// </summary>
    public ActionExecutor(
        IWindowManager windows,
        ISeqClock clock,
        IEventBus events,
        Action<string>? refreshWindow = null,
        ReservedActionDispatcher? reservedActionDispatcher = null)
    {
        _windows = windows;
        _clock = clock;
        _events = events;
        _refreshWindow = refreshWindow;
        _reservedActionDispatcher = reservedActionDispatcher ?? new ReservedActionDispatcher();
    }

    /// <summary>
    /// 在指定窗口执行一个 Action。
    /// </summary>
    public async Task<ActionResult> ExecuteAsync(
        string windowId,
        string actionId,
        JsonElement? parameters = null)
    {
        var effectiveActionId = NormalizeActionId(actionId);

        // 1. 校验窗口存在。
        var window = _windows.Get(windowId);
        if (window == null)
        {
            return ActionResult.Fail($"Window '{windowId}' does not exist");
        }

        // 2. 保留 Action（如 close）由专门分发器处理。
        if (_reservedActionDispatcher.TryDispatch(window, effectiveActionId, parameters, out var reservedResult))
        {
            if (!reservedResult.Success)
            {
                return reservedResult;
            }

            var reservedSeq = _clock.Next();
            return FinalizeExecution(window, actionId, reservedResult, reservedSeq);
        }

        // 3. 执行窗口业务处理器。
        var seq = _clock.Next();
        ActionResult result;

        if (window.Handler == null)
        {
            result = ActionResult.Fail($"Action '{actionId}' does not exist on window '{windowId}'");
        }
        else
        {
            var context = new ActionContext
            {
                Window = window,
                ActionId = actionId,
                Parameters = parameters
            };

            try
            {
                result = await window.Handler.ExecuteAsync(context);
            }
            catch (Exception ex)
            {
                result = ActionResult.Fail($"Action execution failed: {ex.Message}");
            }
        }

        // 4. 统一发布事件并执行窗口后处理。
        return FinalizeExecution(window, actionId, result, seq);
    }

    /// <summary>
    /// 统一处理执行结果落盘：记录 seq、发布事件、处理关闭与刷新。
    /// </summary>
    private ActionResult FinalizeExecution(Window window, string actionId, ActionResult result, int seq)
    {
        result.LogSeq = seq;

        _events.Publish(new ActionExecutedEvent(
            Seq: seq,
            WindowId: window.Id,
            ActionId: actionId,
            Success: result.Success,
            Summary: result.Summary
        ));

        if (result.ShouldClose || window.Options.AutoCloseOnAction)
        {
            _windows.Remove(window.Id);
        }
        else if (result.ShouldRefresh)
        {
            if (_refreshWindow != null)
            {
                _refreshWindow(window.Id);
            }
            else if (_windows is WindowManager wm)
            {
                wm.NotifyUpdated(window.Id);
            }
        }

        return result;
    }

    /// <summary>
    /// 归一化动作 ID（支持 namespace.action）。
    /// </summary>
    private static string NormalizeActionId(string actionId)
    {
        if (string.IsNullOrWhiteSpace(actionId))
        {
            return actionId;
        }

        var dotIndex = actionId.IndexOf('.');
        if (dotIndex <= 0 || dotIndex >= actionId.Length - 1)
        {
            return actionId;
        }

        return actionId[(dotIndex + 1)..];
    }
}

/// <summary>
/// 动作执行完成事件。
/// </summary>
public record ActionExecutedEvent : IEvent
{
    /// <summary>
    /// 事件序号。
    /// </summary>
    public int Seq { get; init; }

    /// <summary>
    /// 目标窗口 ID。
    /// </summary>
    public string WindowId { get; init; }

    /// <summary>
    /// 动作 ID。
    /// </summary>
    public string ActionId { get; init; }

    /// <summary>
    /// 是否成功。
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 可选摘要。
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// 构造事件。
    /// </summary>
    public ActionExecutedEvent(
        int Seq,
        string WindowId,
        string ActionId,
        bool Success,
        string? Summary)
    {
        this.Seq = Seq;
        this.WindowId = WindowId;
        this.ActionId = ActionId;
        this.Success = Success;
        this.Summary = Summary;
    }
}

/// <summary>
/// 应用启动事件。
/// </summary>
public record AppCreatedEvent : IEvent
{
    /// <summary>
    /// 事件序号。
    /// </summary>
    public int Seq { get; init; }

    /// <summary>
    /// 应用名。
    /// </summary>
    public string AppName { get; init; }

    /// <summary>
    /// 启动目标。
    /// </summary>
    public string? Target { get; init; }

    /// <summary>
    /// 是否成功。
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 构造事件。
    /// </summary>
    public AppCreatedEvent(
        int Seq,
        string AppName,
        string? Target,
        bool Success)
    {
        this.Seq = Seq;
        this.AppName = AppName;
        this.Target = Target;
        this.Success = Success;
    }
}
