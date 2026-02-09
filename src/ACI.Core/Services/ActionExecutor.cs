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
    /// 核心依赖服务。
    /// </summary>
    private readonly IWindowManager _windows;
    private readonly ISeqClock _clock;
    private readonly IEventBus _events;

    /// <summary>
    /// 可选的窗口刷新回调。
    /// </summary>
    private readonly Action<string>? _refreshWindow;

    /// <summary>
    /// 创建动作执行器。
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
    /// 在指定窗口上执行一个动作。
    /// </summary>
    public async Task<ActionResult> ExecuteAsync(
        string windowId,
        string actionId,
        JsonElement? parameters = null)
    {
        // 1. 校验窗口与保留动作（close）逻辑。
        var window = _windows.Get(windowId);
        if (window == null)
        {
            return ActionResult.Fail($"Window '{windowId}' does not exist");
        }

        // 系统保留动作：关闭窗口。
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

        // 2. 查找动作定义并执行参数校验。
        var actionDef = window.Actions.FirstOrDefault(a => a.Id == actionId);
        if (actionDef == null)
        {
            return ActionResult.Fail($"Action '{actionId}' does not exist on window '{windowId}'");
        }

        var validationError = ActionParamValidator.Validate(actionDef.ParamsSchema, parameters);
        if (validationError != null)
        {
            return ActionResult.Fail(validationError);
        }

        var seq2 = _clock.Next();

        ActionResult result;
        // 3. 执行动作处理器并统一封装结果。
        if (window.Handler != null)
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
        else
        {
            result = ActionResult.Ok();
        }

        result.LogSeq = seq2;

        _events.Publish(new ActionExecutedEvent(
            Seq: seq2,
            WindowId: windowId,
            ActionId: actionId,
            Success: result.Success,
            Summary: result.Summary
        ));

        // 4. 根据结果刷新或关闭窗口，并返回动作结果。
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
    /// 从参数中提取可选摘要字段。
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
    /// 执行动作 ID。
    /// </summary>
    public string ActionId { get; init; }

    /// <summary>
    /// 是否执行成功。
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 可选摘要信息。
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// 创建动作执行完成事件。
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
/// 应用创建事件。
/// </summary>
public record AppCreatedEvent : IEvent
{
    /// <summary>
    /// 事件序号。
    /// </summary>
    public int Seq { get; init; }

    /// <summary>
    /// 应用名称。
    /// </summary>
    public string AppName { get; init; }

    /// <summary>
    /// 启动目标（可选）。
    /// </summary>
    public string? Target { get; init; }

    /// <summary>
    /// 是否创建成功。
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 创建应用创建事件。
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
