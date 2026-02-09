using System.Text.Json;

namespace ACI.Core.Models;

/// <summary>
/// 支持的参数类型（对应 JSON 值类型）。
/// </summary>
public enum ActionParamKind
{
    /// <summary>
    /// 字符串。
    /// </summary>
    String,

    /// <summary>
    /// 整数。
    /// </summary>
    Integer,

    /// <summary>
    /// 数值（包含整数与浮点数）。
    /// </summary>
    Number,

    /// <summary>
    /// 布尔值。
    /// </summary>
    Boolean,

    /// <summary>
    /// 空值。
    /// </summary>
    Null,

    /// <summary>
    /// 对象。
    /// </summary>
    Object,

    /// <summary>
    /// 数组。
    /// </summary>
    Array
}

/// <summary>
/// 动作用参数结构定义（JSON 风格）。
/// </summary>
public sealed class ActionParamSchema
{
    /// <summary>
    /// 参数类型。
    /// </summary>
    public required ActionParamKind Kind { get; init; }

    /// <summary>
    /// 是否必填。
    /// </summary>
    public bool Required { get; init; } = true;

    /// <summary>
    /// 参数说明。
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 数组元素结构定义（仅 `Array` 类型使用）。
    /// </summary>
    public ActionParamSchema? Items { get; init; }

    /// <summary>
    /// 对象属性结构定义（仅 `Object` 类型使用）。
    /// </summary>
    public Dictionary<string, ActionParamSchema>? Properties { get; init; }

    /// <summary>
    /// 默认值（JSON 片段）。
    /// </summary>
    public JsonElement? Default { get; init; }

    /// <summary>
    /// 渲染为提示词中的参数签名。
    /// </summary>
    public string ToPromptSignature()
    {
        return Kind switch
        {
            ActionParamKind.String => "string",
            ActionParamKind.Integer => "integer",
            ActionParamKind.Number => "number",
            ActionParamKind.Boolean => "boolean",
            ActionParamKind.Null => "null",
            ActionParamKind.Array => $"array<{Items?.ToPromptSignature() ?? "any"}>",
            ActionParamKind.Object => RenderObjectSignature(),
            _ => "any"
        };
    }

    /// <summary>
    /// 渲染对象类型的提示词签名。
    /// </summary>
    private string RenderObjectSignature()
    {
        if (Properties == null || Properties.Count == 0)
        {
            return "object";
        }

        var fields = Properties.Select(kv =>
        {
            var requiredSuffix = kv.Value.Required ? string.Empty : "?";
            return $"{kv.Key}:{kv.Value.ToPromptSignature()}{requiredSuffix}";
        });

        return $"{{{string.Join(", ", fields)}}}";
    }
}
