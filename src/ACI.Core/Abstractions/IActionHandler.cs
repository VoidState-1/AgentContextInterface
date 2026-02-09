using ACI.Core.Models;
using System.Text.Json;

namespace ACI.Core.Abstractions;

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
    public JsonElement? Parameters { get; init; }

    private bool TryGetParameter(string name, out JsonElement value)
    {
        value = default;
        return Parameters.HasValue &&
               Parameters.Value.ValueKind == JsonValueKind.Object &&
               Parameters.Value.TryGetProperty(name, out value);
    }

    /// <summary>
    /// 获取字符串参数
    /// </summary>
    public string? GetString(string name)
    {
        if (!TryGetParameter(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    /// <summary>
    /// 获取整数参数
    /// </summary>
    public int? GetInt(string name)
    {
        if (!TryGetParameter(name, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var parsedNumber) => parsedNumber,
            JsonValueKind.String when int.TryParse(value.GetString(), out var parsedString) => parsedString,
            _ => null
        };
    }

    /// <summary>
    /// 获取布尔参数
    /// </summary>
    public bool GetBool(string name, bool defaultValue = false)
    {
        if (!TryGetParameter(name, out var value)) return defaultValue;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(value.GetString(), out var parsed) && parsed,
            _ => defaultValue
        };
    }
}
