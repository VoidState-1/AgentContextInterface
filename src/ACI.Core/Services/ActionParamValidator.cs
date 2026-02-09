using ACI.Core.Models;
using System.Text.Json;

namespace ACI.Core.Services;

/// <summary>
/// 按参数结构定义校验动作入参。
/// </summary>
public static class ActionParamValidator
{
    /// <summary>
    /// 校验参数是否符合给定结构。
    /// </summary>
    public static string? Validate(ActionParamSchema? schema, JsonElement? parameters)
    {
        if (schema == null)
        {
            return null;
        }

        return ValidateNode(schema, parameters, "params");
    }

    /// <summary>
    /// 递归校验单个节点。
    /// </summary>
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

    /// <summary>
    /// 校验字符串节点。
    /// </summary>
    private static string? ValidateString(JsonElement value, string path)
    {
        return value.ValueKind == JsonValueKind.String
            ? null
            : $"{path} must be string";
    }

    /// <summary>
    /// 校验整数节点。
    /// </summary>
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

    /// <summary>
    /// 校验数值节点。
    /// </summary>
    private static string? ValidateNumber(JsonElement value, string path)
    {
        return value.ValueKind == JsonValueKind.Number
            ? null
            : $"{path} must be number";
    }

    /// <summary>
    /// 校验布尔节点。
    /// </summary>
    private static string? ValidateBoolean(JsonElement value, string path)
    {
        return value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? null
            : $"{path} must be boolean";
    }

    /// <summary>
    /// 校验空值节点。
    /// </summary>
    private static string? ValidateNull(JsonElement value, string path)
    {
        return value.ValueKind == JsonValueKind.Null
            ? null
            : $"{path} must be null";
    }

    /// <summary>
    /// 校验对象节点。
    /// </summary>
    private static string? ValidateObject(ActionParamSchema schema, JsonElement value, string path)
    {
        // 1. 先校验值类型必须为对象。
        if (value.ValueKind != JsonValueKind.Object)
        {
            return $"{path} must be object";
        }

        var props = schema.Properties ?? [];

        // 2. 检查所有必填字段是否存在。
        foreach (var requiredProp in props.Where(p => p.Value.Required))
        {
            if (!value.TryGetProperty(requiredProp.Key, out _))
            {
                return $"{path}.{requiredProp.Key} is required";
            }
        }

        // 3. 遍历输入字段，校验是否允许并递归校验子节点。
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

        // 4. 全部通过后返回成功。
        return null;
    }

    /// <summary>
    /// 校验数组节点。
    /// </summary>
    private static string? ValidateArray(ActionParamSchema schema, JsonElement value, string path)
    {
        // 1. 校验值类型必须为数组。
        if (value.ValueKind != JsonValueKind.Array)
        {
            return $"{path} must be array";
        }

        // 2. 未定义元素结构时只做数组类型校验。
        if (schema.Items == null)
        {
            return null;
        }

        // 3. 逐项递归校验元素。
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

        // 4. 全部通过后返回成功。
        return null;
    }
}
