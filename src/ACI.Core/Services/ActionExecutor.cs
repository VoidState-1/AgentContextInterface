using ACI.Core.Abstractions;
using ACI.Core.Models;
using System.Text.Json;

namespace ACI.Core.Services;

/// <summary>
/// 执行窗口动作并发布执行事件。
/// </summary>
public class ActionExecutor
{
    /// <summary>
    /// 核心依赖。
    /// </summary>
    private readonly IWindowManager _windows;
    private readonly ISeqClock _clock;
    private readonly IEventBus _events;

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
        Action<string>? refreshWindow = null)
    {
        _windows = windows;
        _clock = clock;
        _events = events;
        _refreshWindow = refreshWindow;
    }

    /// <summary>
    /// 在指定窗口执行一个动作。
    /// </summary>
    public async Task<ActionResult> ExecuteAsync(
        string windowId,
        string actionId,
        JsonElement? parameters = null)
    {
        // 1. 校验窗口存在。
        var window = _windows.Get(windowId);
        if (window == null)
        {
            return ActionResult.Fail($"Window '{windowId}' does not exist");
        }

        // 2. 处理系统保留动作 close。
        if (actionId == "close")
        {
            if (!window.Options.Closable)
            {
                return ActionResult.Fail($"Window '{windowId}' cannot be closed");
            }

            var seq = _clock.Next();
            var summary = TryGetSummary(parameters);

            _windows.Remove(windowId);

            var closeResult = ActionResult.Close(summary);
            closeResult.LogSeq = seq;

            _events.Publish(new ActionExecutedEvent(
                Seq: seq,
                WindowId: windowId,
                ActionId: actionId,
                Success: true,
                Summary: summary
            ));

            return closeResult;
        }

        // 3. 通过窗口处理器执行业务动作。
        var seq2 = _clock.Next();
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

        result.LogSeq = seq2;

        _events.Publish(new ActionExecutedEvent(
            Seq: seq2,
            WindowId: windowId,
            ActionId: actionId,
            Success: result.Success,
            Summary: result.Summary
        ));

        // 4. 按动作结果执行关闭或刷新。
        if (result.ShouldClose || window.Options.AutoCloseOnAction)
        {
            _windows.Remove(windowId);
        }
        else if (result.ShouldRefresh)
        {
            if (_refreshWindow != null)
            {
                _refreshWindow(windowId);
            }
            else if (_windows is WindowManager wm)
            {
                wm.NotifyUpdated(windowId);
            }
        }

        return result;
    }

    /// <summary>
    /// 从参数中提取可选摘要。
    /// </summary>
    private static string? TryGetSummary(JsonElement? parameters)
    {
        if (!parameters.HasValue || parameters.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!parameters.Value.TryGetProperty("summary", out var summaryElement))
        {
            return null;
        }

        return summaryElement.ValueKind == JsonValueKind.String
            ? summaryElement.GetString()
            : summaryElement.ToString();
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
