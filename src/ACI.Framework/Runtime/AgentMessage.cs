namespace ACI.Framework.Runtime;

/// <summary>
/// Agent 间通信消息。
/// 这是 MailboxApp 的内部数据模型，底层 MessageChannel 不知道也不关心此类型。
/// </summary>
public class AgentMessage
{
    /// <summary>
    /// 消息唯一 ID。
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 发送方 Agent ID。
    /// </summary>
    public required string FromAgentId { get; init; }

    /// <summary>
    /// 接收方 Agent ID。
    /// </summary>
    public required string ToAgentId { get; init; }

    /// <summary>
    /// 消息内容。
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// 时间戳。
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// 是否已读。
    /// </summary>
    public bool IsRead { get; set; }
}
