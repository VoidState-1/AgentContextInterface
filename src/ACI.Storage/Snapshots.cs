using System.Text.Json.Serialization;
using ACI.Core.Models;
using ACI.Framework.Runtime;

namespace ACI.Storage;

/// <summary>
/// 会话完整快照（顶层序列化单元）。
/// </summary>
public class SessionSnapshot
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
    /// 快照版本号（用于未来兼容性检查）。
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// 快照采集时间。
    /// </summary>
    public DateTime SnapshotAt { get; set; }

    /// <summary>
    /// 各 Agent 快照。
    /// </summary>
    public List<AgentSnapshot> Agents { get; set; } = [];
}

/// <summary>
/// 单个 Agent 的快照。
/// </summary>
public class AgentSnapshot
{
    /// <summary>
    /// Agent 身份配置。
    /// </summary>
    public AgentProfileSnapshot Profile { get; set; } = new();

    /// <summary>
    /// 时钟当前序号（恢复后从此继续）。
    /// </summary>
    public int ClockSeq { get; set; }

    /// <summary>
    /// 上下文时间线条目。
    /// </summary>
    public List<ContextItemSnapshot> ContextItems { get; set; } = [];

    /// <summary>
    /// 各应用快照（使用 ACI.Framework 中的 AppSnapshot）。
    /// </summary>
    public List<AppSnapshot> Apps { get; set; } = [];
}

/// <summary>
/// AgentProfile 的可序列化版本。
/// </summary>
public class AgentProfileSnapshot
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Role { get; set; }
    public string? Model { get; set; }
    public int MaxTokenBudget { get; set; }
    public int MaxResponseTimeSeconds { get; set; }
    public int MaxToolCallTurns { get; set; }

    public static AgentProfileSnapshot From(AgentProfile profile) => new()
    {
        Id = profile.Id,
        Name = profile.Name,
        Role = profile.Role,
        Model = profile.Model,
        MaxTokenBudget = profile.MaxTokenBudget,
        MaxResponseTimeSeconds = profile.MaxResponseTimeSeconds,
        MaxToolCallTurns = profile.MaxToolCallTurns
    };

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
/// ContextItem 的可序列化版本。
/// </summary>
public class ContextItemSnapshot
{
    public string Id { get; set; } = "";

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ContextItemType Type { get; set; }

    public int Seq { get; set; }
    public string Content { get; set; } = "";
    public bool IsObsolete { get; set; }
    public int EstimatedTokens { get; set; }

    public static ContextItemSnapshot From(ContextItem item) => new()
    {
        Id = item.Id,
        Type = item.Type,
        Seq = item.Seq,
        Content = item.Content,
        IsObsolete = item.IsObsolete,
        EstimatedTokens = item.EstimatedTokens
    };

    public ContextItem ToContextItem()
    {
        var item = new ContextItem
        {
            Id = Id,
            Type = Type,
            Content = Content
        };
        // Seq is internal set, accessible via InternalsVisibleTo
        item.Seq = Seq;
        item.IsObsolete = IsObsolete;
        item.EstimatedTokens = EstimatedTokens;
        return item;
    }
}
