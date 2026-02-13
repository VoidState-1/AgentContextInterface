namespace ACI.Framework.Runtime;

/// <summary>
/// App 间消息频道 —— 通用的发布/订阅通信原语。
/// 底层只做消息路由，不理解也不解析消息内容。
/// </summary>
public interface IMessageChannel
{
    /// <summary>
    /// 向指定频道发布消息。
    /// </summary>
    /// <param name="channel">频道名称（如 "agent.mail", "file.opened"）</param>
    /// <param name="data">载荷（JSON 字符串，底层不解析）</param>
    /// <param name="scope">投递范围：Local=仅本 Agent，Session=所有 Agent</param>
    void Post(
        string channel,
        string data,
        MessageScope scope = MessageScope.Local,
        IReadOnlyList<string>? targetAgentIds = null);

    /// <summary>
    /// 订阅指定频道的消息。返回的 IDisposable 用于取消订阅。
    /// </summary>
    IDisposable Subscribe(string channel, Action<ChannelMessage> handler);
}

/// <summary>
/// 消息投递范围。
/// </summary>
public enum MessageScope
{
    /// <summary>仅当前 Agent 内的订阅者</summary>
    Local,

    /// <summary>当前 Session 中所有 Agent 的订阅者</summary>
    Session
}

/// <summary>
/// 频道消息。
/// </summary>
public class ChannelMessage
{
    /// <summary>频道名称</summary>
    public required string Channel { get; init; }

    /// <summary>JSON 载荷（底层不解析）</summary>
    public required string Data { get; init; }

    /// <summary>发送方 Agent ID</summary>
    public required string SourceAgentId { get; init; }

    public IReadOnlyList<string>? TargetAgentIds { get; init; }

    /// <summary>时间戳</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
