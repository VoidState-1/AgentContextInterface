using System.Text.Json;
using System.Text.RegularExpressions;

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
            var toolCall = JsonSerializer.Deserialize<ToolCall>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (toolCall == null) return null;

            if (toolCall.Name == "create")
            {
                return new ParsedAction
                {
                    Type = "create",
                    AppName = toolCall.Arguments?.GetValueOrDefault("name")?.ToString(),
                    Target = toolCall.Arguments?.GetValueOrDefault("target")?.ToString()
                };
            }
            else if (toolCall.Name == "action")
            {
                var windowId = toolCall.Arguments?.GetValueOrDefault("window_id")?.ToString();
                var actionId = toolCall.Arguments?.GetValueOrDefault("action_id")?.ToString();

                if (string.IsNullOrEmpty(windowId) || string.IsNullOrEmpty(actionId))
                {
                    return null;
                }

                Dictionary<string, object>? parameters = null;
                if (toolCall.Arguments?.TryGetValue("params", out var paramsObj) == true && paramsObj is JsonElement paramsElement)
                {
                    parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(paramsElement.GetRawText());
                }

                return new ParsedAction
                {
                    Type = "action",
                    WindowId = windowId,
                    ActionId = actionId,
                    Parameters = parameters
                };
            }
        }
        catch (JsonException)
        {
            // 解析失败
        }

        return null;
    }

    private class ToolCall
    {
        public string? Name { get; set; }
        public Dictionary<string, object>? Arguments { get; set; }
    }
}
