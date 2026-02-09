using ACI.Core.Models;
using System.Text.Json;

namespace ACI.Framework.Runtime;

/// <summary>
/// Builder helpers for action parameter schemas.
/// </summary>
public static class Param
{
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

    public static ActionParamSchema Null(
        bool required = true,
        string? description = null)
        => new()
        {
            Kind = ActionParamKind.Null,
            Required = required,
            Description = description
        };

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

    private static JsonElement? ToDefault(object? defaultValue)
    {
        return defaultValue == null ? null : JsonSerializer.SerializeToElement(defaultValue);
    }
}
