namespace ACI.LLM.Services;

/// <summary>
/// LLM 错误处理器 - 处理重试和降级策略
/// </summary>
public static class LLMErrorHandler
{
    /// <summary>
    /// 是否可以重试
    /// </summary>
    public static bool ShouldRetry(Exception ex)
    {
        // 网络超时或 429/503 等情况可以重试
        if (ex is HttpRequestException httpEx)
        {
            return httpEx.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
                   (int?)httpEx.StatusCode >= 500;
        }

        return ex is TaskCanceledException; // 超时
    }

    /// <summary>
    /// 获取建议的降级模型
    /// </summary>
    public static string? GetFallbackModel(OpenRouterConfig config, string currentModel)
    {
        var nextIndex = config.FallbackModels.IndexOf(currentModel) + 1;
        if (nextIndex < config.FallbackModels.Count)
        {
            return config.FallbackModels[nextIndex];
        }

        return null;
    }
}
