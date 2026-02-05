namespace ContextUI.Server.Dto;

/// <summary>
/// 消息请求
/// </summary>
public class MessageRequest
{
    public required string Message { get; set; }
}

/// <summary>
/// 交互响应
/// </summary>
public class InteractionResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Response { get; set; }
    public ActionInfo? Action { get; set; }
    public ActionResultInfo? ActionResult { get; set; }
    public TokenUsageInfo? Usage { get; set; }
}

/// <summary>
/// 操作信息
/// </summary>
public class ActionInfo
{
    public string? Type { get; set; }
    public string? AppName { get; set; }
    public string? WindowId { get; set; }
    public string? ActionId { get; set; }
}

/// <summary>
/// 操作结果信息
/// </summary>
public class ActionResultInfo
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Summary { get; set; }
}

/// <summary>
/// Token 使用信息
/// </summary>
public class TokenUsageInfo
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}
