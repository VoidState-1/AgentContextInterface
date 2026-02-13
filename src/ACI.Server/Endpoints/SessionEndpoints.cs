using ACI.Server.Dto;
using ACI.Server.Services;
using System.Text;

namespace ACI.Server.Endpoints;

/// <summary>
/// 会话与 Agent 相关端点
/// </summary>
public static class SessionEndpoints
{
    public static void MapSessionEndpoints(this WebApplication app)
    {
        // ========== Session 级端点 ==========
        var sessions = app.MapGroup("/api/sessions")
            .WithTags("Sessions");

        // 获取所有会话
        sessions.MapGet("/", (ISessionManager sessionManager) =>
        {
            var list = sessionManager.GetActiveSessions()
                .Select(id => sessionManager.GetSession(id))
                .Where(s => s != null)
                .Select(s => new
                {
                    s!.SessionId,
                    s.CreatedAt,
                    AgentCount = s.AgentCount,
                    Agents = s.GetAllAgents().Select(a => new
                    {
                        a.AgentId,
                        a.Profile.Name,
                        a.Profile.Role
                    })
                });

            return Results.Ok(list);
        });

        // 创建新会话
        sessions.MapPost("/", (CreateSessionRequest? request, ISessionManager sessionManager) =>
        {
            var session = sessionManager.CreateSession(request);
            return Results.Created($"/api/sessions/{session.SessionId}", new
            {
                session.SessionId,
                session.CreatedAt,
                AgentCount = session.AgentCount,
                Agents = session.GetAllAgents().Select(a => new
                {
                    a.AgentId,
                    a.Profile.Name,
                    a.Profile.Role
                })
            });
        });

        // 获取单个会话
        sessions.MapGet("/{sessionId}", (string sessionId, ISessionManager sessionManager) =>
        {
            var session = sessionManager.GetSession(sessionId);
            if (session == null)
            {
                return Results.NotFound(new { Error = $"会话不存在: {sessionId}" });
            }

            return Results.Ok(new
            {
                session.SessionId,
                session.CreatedAt,
                AgentCount = session.AgentCount,
                Agents = session.GetAllAgents().Select(a => new
                {
                    a.AgentId,
                    a.Profile.Name,
                    a.Profile.Role,
                    WindowCount = a.Windows.GetAll().Count()
                })
            });
        });

        // 关闭会话
        sessions.MapDelete("/{sessionId}", (string sessionId, ISessionManager sessionManager) =>
        {
            var session = sessionManager.GetSession(sessionId);
            if (session == null)
            {
                return Results.NotFound(new { Error = $"会话不存在: {sessionId}" });
            }

            sessionManager.CloseSession(sessionId);
            return Results.NoContent();
        });

        // ========== Agent 级端点 ==========
        var agents = app.MapGroup("/api/sessions/{sessionId}/agents")
            .WithTags("Agents");

        // 获取 Agent 列表
        agents.MapGet("/", (string sessionId, ISessionManager sessionManager) =>
        {
            var session = sessionManager.GetSession(sessionId);
            if (session == null)
            {
                return Results.NotFound(new { Error = $"会话不存在: {sessionId}" });
            }

            return Results.Ok(session.GetAllAgents().Select(a => new
            {
                a.AgentId,
                a.Profile.Name,
                a.Profile.Role,
                WindowCount = a.Windows.GetAll().Count()
            }));
        });

        // 获取 Agent 上下文时间线
        agents.MapGet("/{agentId}/context", (
            string sessionId,
            string agentId,
            bool includeObsolete,
            ISessionManager sessionManager) =>
        {
            var agent = ResolveAgent(sessionManager, sessionId, agentId);
            if (agent == null)
            {
                return Results.NotFound(new { Error = $"Agent 不存在: {agentId}" });
            }

            var items = includeObsolete
                ? agent.Context.GetArchive()
                : agent.Context.GetActive();

            var timeline = items.Select(item =>
            {
                var isWindow = item.Type == Core.Models.ContextItemType.Window;
                var window = isWindow ? agent.Windows.Get(item.Content) : null;

                return new
                {
                    item.Id,
                    Type = item.Type.ToString(),
                    item.Seq,
                    item.IsObsolete,
                    RawContent = item.Content,
                    item.EstimatedTokens,
                    Window = isWindow ? new
                    {
                        WindowId = item.Content,
                        Exists = window != null,
                        window?.AppName,
                        Description = window?.Description?.Render(),
                        Rendered = window?.Render(),
                        Display = window?.Content.Render(),
                        IsCompact = window?.Options.RenderMode == Core.Models.RenderMode.Compact,
                        CreatedAt = window?.Meta.CreatedAt,
                        UpdatedAt = window?.Meta.UpdatedAt
                    } : null
                };
            });

            return Results.Ok(timeline);
        });

        // 获取 Agent 原始上下文文本
        agents.MapGet("/{agentId}/context/raw", (
            string sessionId,
            string agentId,
            bool includeObsolete,
            ISessionManager sessionManager) =>
        {
            var agent = ResolveAgent(sessionManager, sessionId, agentId);
            if (agent == null)
            {
                return Results.NotFound(new { Error = $"Agent 不存在: {agentId}" });
            }

            var items = includeObsolete
                ? agent.Context.GetArchive()
                : agent.Context.GetActive();

            var sb = new StringBuilder();
            foreach (var item in items)
            {
                if (item.Type == Core.Models.ContextItemType.Window)
                {
                    var window = agent.Windows.Get(item.Content);
                    if (window != null)
                    {
                        sb.AppendLine(window.Render());
                    }
                }
                else
                {
                    sb.AppendLine(item.Content);
                }

                sb.AppendLine();
            }

            return Results.Text(sb.ToString().TrimEnd(), "text/plain; charset=utf-8");
        });

        // 获取 LLM 输入快照
        agents.MapGet("/{agentId}/llm-input/raw", (
            string sessionId,
            string agentId,
            ISessionManager sessionManager) =>
        {
            var agent = ResolveAgent(sessionManager, sessionId, agentId);
            if (agent == null)
            {
                return Results.NotFound(new { Error = $"Agent 不存在: {agentId}" });
            }

            var raw = agent.Interaction.GetCurrentLlmInputRaw();
            return Results.Text(raw, "text/plain; charset=utf-8");
        });

        // 获取 Agent 应用列表
        agents.MapGet("/{agentId}/apps", (
            string sessionId,
            string agentId,
            ISessionManager sessionManager) =>
        {
            var agent = ResolveAgent(sessionManager, sessionId, agentId);
            if (agent == null)
            {
                return Results.NotFound(new { Error = $"Agent 不存在: {agentId}" });
            }

            var apps = agent.Host.GetAllApps()
                .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .Select(a => new
                {
                    a.Name,
                    Description = a.AppDescription,
                    a.Tags,
                    IsStarted = agent.Host.IsStarted(a.Name)
                });

            return Results.Ok(apps);
        });
    }

    /// <summary>
    /// 解析 Agent（先找 Session，再找 Agent）
    /// </summary>
    private static AgentContext? ResolveAgent(
        ISessionManager sessionManager, string sessionId, string agentId)
    {
        var session = sessionManager.GetSession(sessionId);
        return session?.GetAgent(agentId);
    }
}
