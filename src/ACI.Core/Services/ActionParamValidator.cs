using ACI.Core.Models;
using System.Text.Json;

namespace ACI.Core.Services;

/// <summary>
/// Validates action params against ActionParamSchema.
/// </summary>
public static class ActionParamValidator
{
    public static string? Validate(ActionParamSchema? schema, JsonElement? parameters)
    {
        if (schema == null)
        {
            return null;
        }

        return ValidateNode(schema, parameters, "params");
    }

    private static string? ValidateNode(ActionParamSchema schema, JsonElement? value, string path)
    {
        if (!value.HasValue)
        {
            return schema.Required ? $"{path} is required" : null;
        }

        return schema.Kind switch
        {
            ActionParamKind.String => ValidateString(value.Value, path),
            ActionParamKind.Integer => ValidateInteger(value.Value, path),
            ActionParamKind.Number => ValidateNumber(value.Value, path),
            ActionParamKind.Boolean => ValidateBoolean(value.Value, path),
            ActionParamKind.Null => ValidateNull(value.Value, path),
            ActionParamKind.Object => ValidateObject(schema, value.Value, path),
            ActionParamKind.Array => ValidateArray(schema, value.Value, path),
            _ => $"{path} has unsupported schema kind"
        };
    }

    private static string? ValidateString(JsonElement value, string path)
    {
        return value.ValueKind == JsonValueKind.String
            ? null
            : $"{path} must be string";
    }

    private static string? ValidateInteger(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Number)
        {
            return $"{path} must be integer";
        }

        return value.TryGetInt64(out _)
            ? null
            : $"{path} must be integer";
    }

    private static string? ValidateNumber(JsonElement value, string path)
    {
        return value.ValueKind == JsonValueKind.Number
            ? null
            : $"{path} must be number";
    }

    private static string? ValidateBoolean(JsonElement value, string path)
    {
        return value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? null
            : $"{path} must be boolean";
    }

    private static string? ValidateNull(JsonElement value, string path)
    {
        return value.ValueKind == JsonValueKind.Null
            ? null
            : $"{path} must be null";
    }

    private static string? ValidateObject(ActionParamSchema schema, JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            return $"{path} must be object";
        }

        var props = schema.Properties ?? [];

        foreach (var requiredProp in props.Where(p => p.Value.Required))
        {
            if (!value.TryGetProperty(requiredProp.Key, out _))
            {
                return $"{path}.{requiredProp.Key} is required";
            }
        }

        foreach (var prop in value.EnumerateObject())
        {
            if (!props.TryGetValue(prop.Name, out var propSchema))
            {
                return $"{path}.{prop.Name} is not allowed";
            }

            var nestedError = ValidateNode(propSchema, prop.Value, $"{path}.{prop.Name}");
            if (nestedError != null)
            {
                return nestedError;
            }
        }

        return null;
    }

    private static string? ValidateArray(ActionParamSchema schema, JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            return $"{path} must be array";
        }

        if (schema.Items == null)
        {
            return null;
        }

        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            var nestedError = ValidateNode(schema.Items, item, $"{path}[{index}]");
            if (nestedError != null)
            {
                return nestedError;
            }

            index++;
        }

        return null;
    }
}
