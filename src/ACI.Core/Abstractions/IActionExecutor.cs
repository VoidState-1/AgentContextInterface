using System.Text.Json;
using ACI.Core.Models;

namespace ACI.Core.Abstractions;

/// <summary>
/// 工具执行器接口。
/// </summary>
public interface IActionExecutor
{
    /// <summary>
    /// 在目标窗口上执行工具。
    /// </summary>
    Task<ActionResult> ExecuteAsync(string windowId, string actionId, JsonElement? parameters = null);
}
