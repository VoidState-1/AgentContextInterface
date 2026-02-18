using System.Text.Json;
using ACI.Core.Models;

namespace ACI.Core.Services;

/// <summary>
/// 保留 Action 分发器。
/// </summary>
public sealed class ReservedActionDispatcher
{
    /// <summary>
    /// 尝试分发保留 Action。
    /// </summary>
    public bool TryDispatch(
        Window window,
        string normalizedActionId,
        JsonElement? parameters,
        out ActionResult result)
    {
        if (!string.Equals(normalizedActionId, "close", StringComparison.OrdinalIgnoreCase))
        {
            result = null!;
            return false;
        }

        if (!window.Options.Closable)
        {
            result = ActionResult.Fail($"Window '{window.Id}' cannot be closed");
            return true;
        }

        var summary = TryGetSummary(parameters);
        result = ActionResult.Close(summary);
        return true;
    }

    /// <summary>
    /// 从参数中提取可选摘要。
    /// </summary>
    private static string? TryGetSummary(JsonElement? parameters)
    {
        if (!parameters.HasValue || parameters.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!parameters.Value.TryGetProperty("summary", out var summaryElement))
        {
            return null;
        }

        return summaryElement.ValueKind == JsonValueKind.String
            ? summaryElement.GetString()
            : summaryElement.ToString();
    }
}
