using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ACI.Core.Services;
using ACI.LLM.Abstractions;
using Microsoft.Extensions.Options;

namespace ACI.LLM.Services;

/// <summary>
/// OpenRouter 客户端实现
/// </summary>
public class OpenRouterClient : ILLMBridge
{
    private readonly HttpClient _httpClient;
    private readonly OpenRouterConfig _config;

    public OpenRouterClient(HttpClient httpClient, IOptions<OpenRouterConfig> config)
    {
        _httpClient = httpClient;
        _config = config.Value;

        if (string.IsNullOrEmpty(_httpClient.BaseAddress?.ToString()))
        {
            // 确保 BaseUrl 以 / 结尾，否则相对路径会替换最后一段而不是追加
            var baseUrl = _config.BaseUrl.TrimEnd('/') + "/";
            _httpClient.BaseAddress = new Uri(baseUrl);
        }

        if (!_httpClient.DefaultRequestHeaders.Contains("Authorization"))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiKey}");
        }

        // OpenRouter 需要这些头部
        if (!_httpClient.DefaultRequestHeaders.Contains("HTTP-Referer"))
        {
            _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://github.com/ACI/ACI");
        }
        if (!_httpClient.DefaultRequestHeaders.Contains("X-Title"))
        {
            _httpClient.DefaultRequestHeaders.Add("X-Title", "ACI");
        }
    }

    /// <summary>
    /// 发送消息到 OpenRouter
    /// </summary>
    public async Task<LLMResponse> SendAsync(IEnumerable<LlmMessage> messages, CancellationToken ct = default)
    {
        int retries = 0;
        string model = _config.DefaultModel;

        while (true)
        {
            try
            {
                var requestBody = new
                {
                    model,
                    messages = messages.Select(m => new
                    {
                        role = m.Role,
                        content = m.Content
                    }).ToArray(),
                    max_tokens = _config.MaxTokens,
                    temperature = _config.Temperature
                };

                var response = await _httpClient.PostAsJsonAsync("chat/completions", requestBody, ct);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(ct);

                    if (retries < _config.MaxRetries)
                    {
                        retries++;
                        var fallback = LLMErrorHandler.GetFallbackModel(_config, model);
                        if (fallback != null) model = fallback;

                        await Task.Delay(1000 * retries, ct);
                        continue;
                    }

                    return LLMResponse.Fail($"OpenRouter API error: {response.StatusCode}, Content: {errorContent}");
                }

                // 先读取原始响应内容
                var responseContent = await response.Content.ReadAsStringAsync(ct);

                OpenRouterResponse? openRouterResponse;
                try
                {
                    openRouterResponse = System.Text.Json.JsonSerializer.Deserialize<OpenRouterResponse>(responseContent);
                }
                catch (System.Text.Json.JsonException jsonEx)
                {
                    // 如果 JSON 解析失败，显示原始响应内容
                    var preview = responseContent.Length > 500 ? responseContent[..500] + "..." : responseContent;
                    return LLMResponse.Fail($"Failed to parse API response as JSON. Raw response: {preview}");
                }

                if (openRouterResponse?.Choices == null || openRouterResponse.Choices.Length == 0)
                {
                    return LLMResponse.Fail($"OpenRouter returned an empty response. Raw: {responseContent}");
                }

                var choice = openRouterResponse.Choices[0];
                return LLMResponse.Ok(
                    choice.Message?.Content ?? "",
                    openRouterResponse.Model,
                    new TokenUsage
                    {
                        PromptTokens = openRouterResponse.Usage?.PromptTokens ?? 0,
                        CompletionTokens = openRouterResponse.Usage?.CompletionTokens ?? 0,
                        TotalTokens = openRouterResponse.Usage?.TotalTokens ?? 0
                    }
                );
            }
            catch (Exception ex) when (retries < _config.MaxRetries && LLMErrorHandler.ShouldRetry(ex))
            {
                retries++;
                await Task.Delay(1000 * retries, ct);
            }
            catch (Exception ex)
            {
                return LLMResponse.Fail($"Exception during LLM call: {ex.Message}");
            }
        }
    }

    #region 内部模型

    private class OpenRouterResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("choices")]
        public Choice[] Choices { get; set; } = [];

        [JsonPropertyName("usage")]
        public UsageInfo? Usage { get; set; }
    }

    private class Choice
    {
        [JsonPropertyName("message")]
        public MessageInfo? Message { get; set; }

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    private class MessageInfo
    {
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    private class UsageInfo
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
    }

    #endregion
}
