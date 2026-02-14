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
    public string Id { get; set; } = "";

    /// <summary>
    /// 发送方 Agent ID。
    /// </summary>
    public string FromAgentId { get; set; } = "";

    /// <summary>
    /// 接收方 Agent ID。
    /// </summary>
    public string ToAgentId { get; set; } = "";

    /// <summary>
    /// 消息内容。
    /// </summary>
    public string Content { get; set; } = "";

    /// <summary>
    /// 时间戳。
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 是否已读。
    /// </summary>
    public bool IsRead { get; set; }
}
