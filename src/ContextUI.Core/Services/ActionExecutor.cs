using ContextUI.Core.Abstractions;
using ContextUI.Core.Models;

namespace ContextUI.Core.Services;

/// <summary>
/// 操作执行器 - 负责执行窗口操作
/// </summary>
public class ActionExecutor
{
    private readonly IWindowManager _windows;
    private readonly ILogManager _logs;

    public ActionExecutor(IWindowManager windows, ILogManager logs)
    {
        _windows = windows;
        _logs = logs;
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

        // 4. 执行操作
        ActionResult result;
        if (window.Handler != null)
        {
            var context = new ActionContext
            {
                Window = window,
                ActionId = actionId,
                Parameters = parameters
            };
            result = await window.Handler.ExecuteAsync(context);
        }
        else
        {
            // 无处理器，默认成功
            result = ActionResult.Ok();
        }

        // 5. 记录日志
        var logEntry = new ActionLogEntry
        {
            WindowId = windowId,
            ActionId = actionId,
            Success = result.Success,
            Summary = result.Summary,
            IsPersistent = result.Summary != null
        };
        var seq = _logs.Append(logEntry);
        result.LogSeq = seq;

        // 6. 处理结果
        if (result.ShouldClose || window.Options.AutoCloseOnAction)
        {
            _windows.Remove(windowId);
        }
        else if (result.ShouldRefresh && _windows is WindowManager wm)
        {
            wm.NotifyUpdated(windowId);
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
