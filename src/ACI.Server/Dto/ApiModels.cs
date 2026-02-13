using ACI.Framework.Runtime;

namespace ACI.Server.Dto;

/// <summary>
/// 创建 Session 请求
/// </summary>
public class CreateSessionRequest
{
    /// <summary>
    /// Agent 配置列表。为空时默认创建单 Agent。
    /// </summary>
    public List<AgentProfileDto>? Agents { get; set; }
}

/// <summary>
/// Agent 配置 DTO
/// </summary>
public class AgentProfileDto
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string? Role { get; set; }
    public string? Model { get; set; }
    public int MaxTokenBudget { get; set; }
    public int MaxResponseTimeSeconds { get; set; }
    public int MaxToolCallTurns { get; set; }

    public AgentProfile ToProfile() => new()
    {
        Id = Id,
        Name = Name,
        Role = Role,
        Model = Model,
        MaxTokenBudget = MaxTokenBudget,
        MaxResponseTimeSeconds = MaxResponseTimeSeconds,
        MaxToolCallTurns = MaxToolCallTurns
    };
}

/// <summary>
/// 消息请求
/// </summary>
public class MessageRequest
{
    public required string Message { get; set; }
}

/// <summary>
/// 手动模拟 AI 输出请求
/// </summary>
public class SimulateRequest
{
    public required string AssistantOutput { get; set; }
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
    public List<InteractionStepInfo>? Steps { get; set; }
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
/// 交互执行步骤
/// </summary>
public class InteractionStepInfo
{
    public required string CallId { get; set; }
    public required string WindowId { get; set; }
    public required string ActionId { get; set; }
    public required string ResolvedMode { get; set; }
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Summary { get; set; }
    public string? TaskId { get; set; }
    public int Turn { get; set; }
    public int Index { get; set; }
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
