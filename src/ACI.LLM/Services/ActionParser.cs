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
    public static ParsedActionBatch? Parse(string content)
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
            var calls = new List<ParsedAction>();
            if (toolCall.Calls.Count > 0)
            {
                foreach (var call in toolCall.Calls)
                {
                    if (string.IsNullOrWhiteSpace(call.WindowId) || string.IsNullOrWhiteSpace(call.ActionId))
                    {
                        return null;
                    }

                    calls.Add(new ParsedAction
                    {
                        WindowId = call.WindowId!,
                        ActionId = call.ActionId!,
                        Parameters = ReadParams(call.Params)
                    });
                }
            }
            else if (!string.IsNullOrWhiteSpace(toolCall.WindowId) &&
                     !string.IsNullOrWhiteSpace(toolCall.ActionId))
            {
                // 兼容单条结构：{"window_id":"...","action_id":"...","params":{...}}
                calls.Add(new ParsedAction
                {
                    WindowId = toolCall.WindowId!,
                    ActionId = toolCall.ActionId!,
                    Parameters = ReadParams(toolCall.Params)
                });
            }

            if (calls.Count == 0)
            {
                return null;
            }

            return new ParsedActionBatch
            {
                Calls = calls
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
        [JsonPropertyName("calls")]
        public List<ToolActionItem> Calls { get; set; } = [];

        [JsonPropertyName("window_id")]
        public string? WindowId { get; set; }

        [JsonPropertyName("action_id")]
        public string? ActionId { get; set; }

        [JsonPropertyName("params")]
        public JsonElement? Params { get; set; }
    }

    private class ToolActionItem
    {
        [JsonPropertyName("window_id")]
        public string? WindowId { get; set; }

        [JsonPropertyName("action_id")]
        public string? ActionId { get; set; }

        [JsonPropertyName("params")]
        public JsonElement? Params { get; set; }
    }

    private static Dictionary<string, object>? ReadParams(JsonElement? paramsElement)
    {
        if (!paramsElement.HasValue || paramsElement.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return JsonSerializer.Deserialize<Dictionary<string, object>>(paramsElement.Value.GetRawText());
    }
}
