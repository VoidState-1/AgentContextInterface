using System.Text.Json;

namespace ACI.Core.Models;

/// <summary>
/// Supported JSON parameter kinds.
/// </summary>
public enum ActionParamKind
{
    String,
    Integer,
    Number,
    Boolean,
    Null,
    Object,
    Array
}

/// <summary>
/// JSON-like schema used by actions.
/// </summary>
public sealed class ActionParamSchema
{
    public required ActionParamKind Kind { get; init; }

    public bool Required { get; init; } = true;

    public string? Description { get; init; }

    public ActionParamSchema? Items { get; init; }

    public Dictionary<string, ActionParamSchema>? Properties { get; init; }

    public JsonElement? Default { get; init; }

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
