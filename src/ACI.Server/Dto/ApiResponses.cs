using ACI.Core.Models;
using ACI.Server.Services;

namespace ACI.Server.Dto;

/// <summary>
/// 统一错误响应。
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// 错误信息。
    /// </summary>
    public required string Error { get; init; }
}

/// <summary>
/// Agent 摘要响应。
/// </summary>
public class AgentSummaryResponse
{
    /// <summary>
    /// Agent ID。
    /// </summary>
    public required string AgentId { get; init; }

    /// <summary>
    /// Agent 名称。
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Agent 角色。
    /// </summary>
    public string? Role { get; init; }

    /// <summary>
    /// 当前窗口数。
    /// </summary>
    public int WindowCount { get; init; }
}

/// <summary>
/// Session 摘要响应。
/// </summary>
public class SessionSummaryResponse
{
    /// <summary>
    /// Session ID。
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// 创建时间（UTC）。
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Agent 数量。
    /// </summary>
    public int AgentCount { get; init; }

    /// <summary>
    /// Agent 列表。
    /// </summary>
    public List<AgentSummaryResponse> Agents { get; init; } = [];
}

/// <summary>
/// App 摘要响应。
/// </summary>
public class AppSummaryResponse
{
    /// <summary>
    /// App 名称。
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// App 描述。
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// App 标签。
    /// </summary>
    public List<string> Tags { get; init; } = [];

    /// <summary>
    /// 是否已启动。
    /// </summary>
    public bool IsStarted { get; init; }
}

/// <summary>
/// Window 摘要响应。
/// </summary>
public class WindowSummaryResponse
{
    /// <summary>
    /// 窗口 ID。
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 窗口描述。
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 窗口渲染内容。
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// 窗口可见命名空间。
    /// </summary>
    public List<string> Namespaces { get; init; } = [];

    /// <summary>
    /// 所属应用名。
    /// </summary>
    public string? AppName { get; init; }

    /// <summary>
    /// 创建序号。
    /// </summary>
    public int CreatedAt { get; init; }

    /// <summary>
    /// 最近更新序号。
    /// </summary>
    public int UpdatedAt { get; init; }
}

/// <summary>
/// Context 时间线中的窗口投影。
/// </summary>
public class ContextTimelineWindowResponse
{
    /// <summary>
    /// 窗口 ID。
    /// </summary>
    public required string WindowId { get; init; }

    /// <summary>
    /// 窗口是否仍存在。
    /// </summary>
    public bool Exists { get; init; }

    /// <summary>
    /// 所属应用名。
    /// </summary>
    public string? AppName { get; init; }

    /// <summary>
    /// 窗口描述。
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 窗口完整渲染。
    /// </summary>
    public string? Rendered { get; init; }

    /// <summary>
    /// 仅内容渲染。
    /// </summary>
    public string? Display { get; init; }

    /// <summary>
    /// 是否为紧凑模式。
    /// </summary>
    public bool? IsCompact { get; init; }

    /// <summary>
    /// 创建序号。
    /// </summary>
    public int? CreatedAt { get; init; }

    /// <summary>
    /// 最近更新序号。
    /// </summary>
    public int? UpdatedAt { get; init; }
}

/// <summary>
/// Context 时间线响应。
/// </summary>
public class ContextTimelineItemResponse
{
    /// <summary>
    /// 条目 ID。
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 条目类型。
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// 序号。
    /// </summary>
    public int Seq { get; init; }

    /// <summary>
    /// 是否已过期。
    /// </summary>
    public bool IsObsolete { get; init; }

    /// <summary>
    /// 原始内容。
    /// </summary>
    public required string RawContent { get; init; }

    /// <summary>
    /// 估算 token。
    /// </summary>
    public int EstimatedTokens { get; init; }

    /// <summary>
    /// 当条目类型为 Window 时，附带窗口投影。
    /// </summary>
    public ContextTimelineWindowResponse? Window { get; init; }
}

/// <summary>
/// 执行窗口 Action 的响应。
/// </summary>
public class WindowActionInvokeResponse
{
    /// <summary>
    /// 是否执行成功。
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 执行消息。
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// 可展示摘要。
    /// </summary>
    public string? Summary { get; init; }
}

/// <summary>
/// 保存 Session 的响应。
/// </summary>
public class SaveSessionResponse
{
    /// <summary>
    /// Session ID。
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// 是否保存成功。
    /// </summary>
    public bool Saved { get; init; }

    /// <summary>
    /// 保存时间（UTC）。
    /// </summary>
    public DateTime SavedAt { get; init; }
}

/// <summary>
/// 健康检查响应。
/// </summary>
public class HealthResponse
{
    /// <summary>
    /// 服务状态。
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// 当前时间（UTC）。
    /// </summary>
    public DateTime Time { get; init; }
}

/// <summary>
/// API 响应映射器。
/// </summary>
public static class ApiResponseMapper
{
    /// <summary>
    /// 将 Session 映射为 SessionSummaryResponse。
    /// </summary>
    public static SessionSummaryResponse ToSessionSummary(Session session)
    {
        return new SessionSummaryResponse
        {
            SessionId = session.SessionId,
            CreatedAt = session.CreatedAt,
            AgentCount = session.AgentCount,
            Agents = session.GetAllAgents()
                .Select(ToAgentSummary)
                .ToList()
        };
    }

    /// <summary>
    /// 将 Agent 映射为 AgentSummaryResponse。
    /// </summary>
    public static AgentSummaryResponse ToAgentSummary(AgentContext agent)
    {
        return new AgentSummaryResponse
        {
            AgentId = agent.AgentId,
            Name = agent.Profile.Name,
            Role = agent.Profile.Role,
            WindowCount = agent.Windows.GetAll().Count()
        };
    }

    /// <summary>
    /// 将 Window 映射为 WindowSummaryResponse。
    /// </summary>
    public static WindowSummaryResponse ToWindowSummary(Window window)
    {
        return new WindowSummaryResponse
        {
            Id = window.Id,
            Description = window.Description?.Render(),
            Content = window.Render(),
            Namespaces = window.NamespaceRefs.ToList(),
            AppName = window.AppName,
            CreatedAt = window.Meta.CreatedAt,
            UpdatedAt = window.Meta.UpdatedAt
        };
    }

    /// <summary>
    /// 将上下文条目映射为时间线响应。
    /// </summary>
    public static ContextTimelineItemResponse ToContextTimelineItem(ContextItem item, AgentContext agent)
    {
        var isWindow = item.Type == ContextItemType.Window;
        var window = isWindow ? agent.Windows.Get(item.Content) : null;

        return new ContextTimelineItemResponse
        {
            Id = item.Id,
            Type = item.Type.ToString(),
            Seq = item.Seq,
            IsObsolete = item.IsObsolete,
            RawContent = item.Content,
            EstimatedTokens = item.EstimatedTokens,
            Window = isWindow
                ? new ContextTimelineWindowResponse
                {
                    WindowId = item.Content,
                    Exists = window != null,
                    AppName = window?.AppName,
                    Description = window?.Description?.Render(),
                    Rendered = window?.Render(),
                    Display = window?.Content.Render(),
                    IsCompact = window?.Options.RenderMode == RenderMode.Compact,
                    CreatedAt = window?.Meta.CreatedAt,
                    UpdatedAt = window?.Meta.UpdatedAt
                }
                : null
        };
    }
}
