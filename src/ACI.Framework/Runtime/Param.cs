using ACI.Core.Models;
using System.Text.Json;

namespace ACI.Framework.Runtime;

/// <summary>
/// 动作参数结构构建辅助类。
/// </summary>
public static class Param
{
    /// <summary>
    /// 创建字符串参数结构。
    /// </summary>
    public static ActionParamSchema String(
        bool required = true,
        string? description = null,
        object? defaultValue = null)
        => new()
        {
            Kind = ActionParamKind.String,
            Required = required,
            Description = description,
            Default = ToDefault(defaultValue)
        };

    /// <summary>
    /// 创建整数参数结构。
    /// </summary>
    public static ActionParamSchema Integer(
        bool required = true,
        string? description = null,
        object? defaultValue = null)
        => new()
        {
            Kind = ActionParamKind.Integer,
            Required = required,
            Description = description,
            Default = ToDefault(defaultValue)
        };

    /// <summary>
    /// 创建数值参数结构。
    /// </summary>
    public static ActionParamSchema Number(
        bool required = true,
        string? description = null,
        object? defaultValue = null)
        => new()
        {
            Kind = ActionParamKind.Number,
            Required = required,
            Description = description,
            Default = ToDefault(defaultValue)
        };

    /// <summary>
    /// 创建布尔参数结构。
    /// </summary>
    public static ActionParamSchema Boolean(
        bool required = true,
        string? description = null,
        object? defaultValue = null)
        => new()
        {
            Kind = ActionParamKind.Boolean,
            Required = required,
            Description = description,
            Default = ToDefault(defaultValue)
        };

    /// <summary>
    /// 创建空值参数结构。
    /// </summary>
    public static ActionParamSchema Null(
        bool required = true,
        string? description = null)
        => new()
        {
            Kind = ActionParamKind.Null,
            Required = required,
            Description = description
        };

    /// <summary>
    /// 创建数组参数结构。
    /// </summary>
    public static ActionParamSchema Array(
        ActionParamSchema items,
        bool required = true,
        string? description = null)
        => new()
        {
            Kind = ActionParamKind.Array,
            Required = required,
            Description = description,
            Items = items
        };

    /// <summary>
    /// 创建对象参数结构。
    /// </summary>
    public static ActionParamSchema Object(
        Dictionary<string, ActionParamSchema> properties,
        bool required = true,
        string? description = null)
        => new()
        {
            Kind = ActionParamKind.Object,
            Required = required,
            Description = description,
            Properties = properties
        };

    /// <summary>
    /// 将默认值转换为 `JsonElement`。
    /// </summary>
    private static JsonElement? ToDefault(object? defaultValue)
    {
        return defaultValue == null ? null : JsonSerializer.SerializeToElement(defaultValue);
    }
}
