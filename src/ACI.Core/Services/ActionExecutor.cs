using ACI.Core.Abstractions;
using ACI.Core.Models;

namespace ACI.Core.Services;

/// <summary>
/// 操作执行器 - 负责执行窗口操作
/// </summary>
public class ActionExecutor
{
    private readonly IWindowManager _windows;
    private readonly ISeqClock _clock;
    private readonly IEventBus _events;
    private readonly Action<string>? _refreshWindow;

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
    /// 执行操作
    /// </summary>
    public async Task<ActionResult> ExecuteAsync(
        string windowId,
        string actionId,
        Dictionary<string, object>? parameters = null)
    {
        // 1. 获取窗口
        var window = _windows.Get(windowId);
        if (window == null)
        {
            return ActionResult.Fail($"窗口 '{windowId}' 不存在");
        }

        // close 是系统保留操作，允许窗口不显式声明
        if (actionId == "close")
        {
            if (!window.Options.Closable)
            {
                return ActionResult.Fail($"窗口 '{windowId}' 不允许关闭");
            }

            var seq1 = _clock.Next();
            var summary = parameters?.TryGetValue("summary", out var summaryObj) == true
                ? summaryObj?.ToString()
                : null;

            _windows.Remove(windowId);

            var closeResult = ActionResult.Close(summary);
            closeResult.LogSeq = seq1;

            _events.Publish(new ActionExecutedEvent(
                Seq: seq1,
                WindowId: windowId,
                ActionId: actionId,
                Success: true,
                Summary: summary
            ));

            return closeResult;
        }

        // 2. 获取操作定义
        var actionDef = window.Actions.FirstOrDefault(a => a.Id == actionId);
        if (actionDef == null)
        {
            return ActionResult.Fail($"操作 '{actionId}' 在窗口 '{windowId}' 中不存在");
        }

        // 3. 验证参数
        var validationError = ValidateParameters(actionDef, parameters);
        if (validationError != null)
        {
            return ActionResult.Fail(validationError);
        }

        // 4. 分配 seq
        var seq = _clock.Next();

        // 5. 执行操作
        ActionResult result;
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
                result = ActionResult.Fail($"操作执行异常: {ex.Message}");
            }
        }
        else
        {
            // 无处理器，默认成功
            result = ActionResult.Ok();
        }

        result.LogSeq = seq;

        // 6. 发布事件（供日志系统订阅）
        _events.Publish(new ActionExecutedEvent(
            Seq: seq,
            WindowId: windowId,
            ActionId: actionId,
            Success: result.Success,
            Summary: result.Summary
        ));

        // 7. 处理结果
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
    /// 验证参数
    /// </summary>
    private static string? ValidateParameters(
        ActionDefinition actionDef,
        Dictionary<string, object>? parameters)
    {
        foreach (var param in actionDef.Parameters.Where(p => p.Required))
        {
            if (parameters == null || !parameters.ContainsKey(param.Name))
            {
                return $"缺少必需参数: {param.Name}";
            }
        }
        return null;
    }
}

/// <summary>
/// 操作执行事件（供日志系统订阅）
/// </summary>
public record ActionExecutedEvent(
    int Seq,
    string WindowId,
    string ActionId,
    bool Success,
    string? Summary
) : IEvent;

/// <summary>
/// 应用创建事件
/// </summary>
public record AppCreatedEvent(
    int Seq,
    string AppName,
    string? Target,
    bool Success
) : IEvent;
