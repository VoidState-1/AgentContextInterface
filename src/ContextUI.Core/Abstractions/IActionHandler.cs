using ContextUI.Core.Models;

namespace ContextUI.Core.Abstractions;

/// <summary>
/// 操作处理器接口
/// </summary>
public interface IActionHandler
{
    /// <summary>
    /// 执行操作
    /// </summary>
    Task<ActionResult> ExecuteAsync(ActionContext context);
}

/// <summary>
/// 操作执行上下文
/// </summary>
public sealed class ActionContext
{
    /// <summary>
    /// 目标窗口
    /// </summary>
    public required Window Window { get; init; }

    /// <summary>
    /// 操作 ID
    /// </summary>
    public required string ActionId { get; init; }

    /// <summary>
    /// 操作参数
    /// </summary>
    public Dictionary<string, object>? Parameters { get; init; }

    /// <summary>
    /// 获取字符串参数
    /// </summary>
    public string? GetString(string name)
        => Parameters?.TryGetValue(name, out var val) == true ? val?.ToString() : null;

    /// <summary>
    /// 获取整数参数
    /// </summary>
    public int? GetInt(string name)
    {
        if (Parameters?.TryGetValue(name, out var val) != true) return null;
        return val switch
        {
            int i => i,
            long l => (int)l,
            string s when int.TryParse(s, out var parsedString) => parsedString,
            System.Text.Json.JsonElement je when
                je.ValueKind == System.Text.Json.JsonValueKind.Number &&
                je.TryGetInt32(out var i) => i,
            System.Text.Json.JsonElement je when
                je.ValueKind == System.Text.Json.JsonValueKind.String &&
                int.TryParse(je.GetString(), out var parsedElementString) => parsedElementString,
            _ => null
        };
    }

    /// <summary>
    /// 获取布尔参数
    /// </summary>
    public bool GetBool(string name, bool defaultValue = false)
    {
        if (Parameters?.TryGetValue(name, out var val) != true) return defaultValue;
        return val switch
        {
            bool b => b,
            string s => bool.TryParse(s, out var parsed) && parsed,
            System.Text.Json.JsonElement je => je.ValueKind == System.Text.Json.JsonValueKind.True,
            _ => defaultValue
        };
    }
}
