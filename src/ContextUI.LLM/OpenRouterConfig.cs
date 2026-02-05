namespace ContextUI.LLM;

/// <summary>
/// OpenRouter 配置
/// </summary>
public class OpenRouterConfig
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "OpenRouter";

    /// <summary>
    /// API 基础 URL
    /// </summary>
    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";

    /// <summary>
    /// API Key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// 默认模型
    /// </summary>
    public string DefaultModel { get; set; } = "anthropic/claude-3.5-sonnet";

    /// <summary>
    /// 备选模型列表
    /// </summary>
    public List<string> FallbackModels { get; set; } =
    [
        "openai/gpt-4-turbo",
        "google/gemini-pro"
    ];

    /// <summary>
    /// 最大 Token 数
    /// </summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>
    /// Temperature
    /// </summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>
    /// 请求超时（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// 重试次数
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}
