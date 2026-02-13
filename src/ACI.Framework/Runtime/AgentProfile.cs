namespace ACI.Framework.Runtime;

/// <summary>
/// Agent 身份配置。
/// 描述一个 Agent 的身份、角色、模型和资源预算。
/// </summary>
public class AgentProfile
{
    /// <summary>
    /// Agent 唯一标识（如 "planner", "coder", "default"）。
    /// 在同一个 Session 中必须唯一。
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Agent 显示名称。
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Agent 角色描述（注入 system prompt，告诉 LLM 自己的职责）。
    /// </summary>
    public string? Role { get; init; }

    /// <summary>
    /// 模型覆盖。null 则使用全局默认模型。
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// 单次交互 Token 预算上限。0 = 不限制。
    /// </summary>
    public int MaxTokenBudget { get; init; } = 0;

    /// <summary>
    /// 响应时间预算（秒）。0 = 不限制。
    /// </summary>
    public int MaxResponseTimeSeconds { get; init; } = 0;

    /// <summary>
    /// 单次交互最大工具调用轮次。0 = 使用系统默认值。
    /// </summary>
    public int MaxToolCallTurns { get; init; } = 0;

    /// <summary>
    /// 创建单 Agent 场景下的默认 Profile。
    /// </summary>
    public static AgentProfile Default() => new()
    {
        Id = "default",
        Name = "Default Agent"
    };
}
