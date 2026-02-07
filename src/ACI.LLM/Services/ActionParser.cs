using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;

namespace ACI.LLM.Services;

/// <summary>
/// 操作解析器 - 从 AI 响应中提取并解析操作指令
/// </summary>
public static class ActionParser
{
    private static readonly Regex ToolCallRegex = new(@"<tool_call>(.*?)</tool_call>", RegexOptions.Singleline);

    /// <summary>
    /// 解析 AI 响应
    /// </summary>
    public static ParsedAction? Parse(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        var match = ToolCallRegex.Match(content);
        if (!match.Success) return null;

        try
        {
            var json = match.Groups[1].Value.Trim();
            var toolCall = JsonSerializer.Deserialize<ToolActionCall>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (toolCall == null) return null;
            if (string.IsNullOrWhiteSpace(toolCall.WindowId) || string.IsNullOrWhiteSpace(toolCall.ActionId))
            {
                return null;
            }

            Dictionary<string, object>? parameters = null;
            if (toolCall.Params.HasValue && toolCall.Params.Value.ValueKind == JsonValueKind.Object)
            {
                parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(toolCall.Params.Value.GetRawText());
            }

            return new ParsedAction
            {
                WindowId = toolCall.WindowId!,
                ActionId = toolCall.ActionId!,
                Parameters = parameters
            };
        }
        catch (JsonException)
        {
            // 解析失败
        }

        return null;
    }

    private class ToolActionCall
    {
        [JsonPropertyName("window_id")]
        public string? WindowId { get; set; }

        [JsonPropertyName("action_id")]
        public string? ActionId { get; set; }

        [JsonPropertyName("params")]
        public JsonElement? Params { get; set; }
    }
}
