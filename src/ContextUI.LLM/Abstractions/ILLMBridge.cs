using ContextUI.Core.Services;

namespace ContextUI.LLM.Abstractions;

/// <summary>
/// LLM Bridge 接口 - 与 LLM 通信
/// </summary>
public interface ILLMBridge
{
    /// <summary>
    /// 发送消息给 LLM，获取响应
    /// </summary>
    Task<LLMResponse> SendAsync(IEnumerable<LlmMessage> messages, CancellationToken ct = default);
}

/// <summary>
/// LLM 响应
/// </summary>
public class LLMResponse
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 响应内容
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// 使用的模型
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Token 使用量
    /// </summary>
    public TokenUsage? Usage { get; set; }

    /// <summary>
    /// 创建成功响应
    /// </summary>
    public static LLMResponse Ok(string content, string? model = null, TokenUsage? usage = null) =>
        new() { Success = true, Content = content, Model = model, Usage = usage };

    /// <summary>
    /// 创建失败响应
    /// </summary>
    public static LLMResponse Fail(string error) =>
        new() { Success = false, Error = error };
}

/// <summary>
/// Token 使用量
/// </summary>
public class TokenUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}
