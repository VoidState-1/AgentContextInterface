namespace ACI.Storage;

/// <summary>
/// 会话存储接口（存储无关设计）。
/// 所有方法均接受 CancellationToken，以支持异步 I/O。
/// </summary>
public interface ISessionStore
{
    /// <summary>
    /// 保存会话快照。覆盖同 ID 的旧快照。
    /// </summary>
    Task SaveAsync(SessionSnapshot snapshot, CancellationToken ct = default);

    /// <summary>
    /// 加载指定会话 ID 的快照。不存在则返回 null。
    /// </summary>
    Task<SessionSnapshot?> LoadAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// 删除指定会话 ID 的快照。
    /// </summary>
    Task DeleteAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// 列出所有已保存的会话摘要。
    /// </summary>
    Task<IReadOnlyList<SessionSummary>> ListAsync(CancellationToken ct = default);

    /// <summary>
    /// 判断指定会话 ID 的快照是否存在。
    /// </summary>
    Task<bool> ExistsAsync(string sessionId, CancellationToken ct = default);
}

/// <summary>
/// 已保存会话的摘要信息（用于列表展示）。
/// </summary>
public class SessionSummary
{
    /// <summary>
    /// 会话 ID。
    /// </summary>
    public string SessionId { get; set; } = "";

    /// <summary>
    /// 会话创建时间。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 快照保存时间。
    /// </summary>
    public DateTime SnapshotAt { get; set; }

    /// <summary>
    /// Agent 数量。
    /// </summary>
    public int AgentCount { get; set; }
}
