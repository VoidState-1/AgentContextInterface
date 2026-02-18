using System.Text;
using ACI.Core.Models;
using ACI.Server.Dto;
using ACI.Server.Services;

namespace ACI.Server.Endpoints;

/// <summary>
/// Session / Agent 相关端点。
/// </summary>
public static class SessionEndpoints
{
    /// <summary>
    /// 注册 Session / Agent 端点。
    /// </summary>
    public static void MapSessionEndpoints(this WebApplication app)
    {
        var sessions = app.MapGroup("/api/sessions")
            .WithTags("Sessions");

        sessions.MapGet("/", (ISessionManager sessionManager) =>
        {
            var list = sessionManager.GetActiveSessions()
                .Select(id => sessionManager.GetSession(id))
                .Where(s => s != null)
                .Select(s => ApiResponseMapper.ToSessionSummary(s!))
                .ToList();

            return Results.Ok(list);
        });

        sessions.MapPost("/", (CreateSessionRequest? request, ISessionManager sessionManager) =>
        {
            try
            {
                var session = sessionManager.CreateSession(request);
                return Results.Created($"/api/sessions/{session.SessionId}", ApiResponseMapper.ToSessionSummary(session));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ErrorResponse { Error = ex.Message });
            }
        });

        sessions.MapGet("/{sessionId}", (string sessionId, ISessionManager sessionManager) =>
        {
            var session = sessionManager.GetSession(sessionId);
            if (session == null)
            {
                return Results.NotFound(new ErrorResponse { Error = $"Session not found: {sessionId}" });
            }

            return Results.Ok(ApiResponseMapper.ToSessionSummary(session));
        });

        sessions.MapDelete("/{sessionId}", (string sessionId, ISessionManager sessionManager) =>
        {
            var session = sessionManager.GetSession(sessionId);
            if (session == null)
            {
                return Results.NotFound(new ErrorResponse { Error = $"Session not found: {sessionId}" });
            }

            sessionManager.CloseSession(sessionId);
            return Results.NoContent();
        });

        var agents = app.MapGroup("/api/sessions/{sessionId}/agents")
            .WithTags("Agents");

        agents.MapGet("/", (string sessionId, ISessionManager sessionManager) =>
        {
            var session = sessionManager.GetSession(sessionId);
            if (session == null)
            {
                return Results.NotFound(new ErrorResponse { Error = $"Session not found: {sessionId}" });
            }

            var agentsList = session.GetAllAgents()
                .Select(ApiResponseMapper.ToAgentSummary)
                .ToList();

            return Results.Ok(agentsList);
        });

        agents.MapGet("/{agentId}/context", (
            string sessionId,
            string agentId,
            bool includeObsolete,
            ISessionManager sessionManager) =>
        {
            var agent = ResolveAgent(sessionManager, sessionId, agentId);
            if (agent == null)
            {
                return Results.NotFound(new ErrorResponse { Error = $"Agent not found: {agentId}" });
            }

            var items = includeObsolete
                ? agent.Context.GetArchive()
                : agent.Context.GetActive();

            var timeline = items
                .Select(item => ApiResponseMapper.ToContextTimelineItem(item, agent))
                .ToList();

            return Results.Ok(timeline);
        });

        agents.MapGet("/{agentId}/context/raw", (
            string sessionId,
            string agentId,
            bool includeObsolete,
            ISessionManager sessionManager) =>
        {
            var agent = ResolveAgent(sessionManager, sessionId, agentId);
            if (agent == null)
            {
                return Results.NotFound(new ErrorResponse { Error = $"Agent not found: {agentId}" });
            }

            var items = includeObsolete
                ? agent.Context.GetArchive()
                : agent.Context.GetActive();

            var sb = new StringBuilder();
            foreach (var item in items)
            {
                if (item.Type == ContextItemType.Window)
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

        agents.MapGet("/{agentId}/llm-input/raw", (
            string sessionId,
            string agentId,
            ISessionManager sessionManager) =>
        {
            var agent = ResolveAgent(sessionManager, sessionId, agentId);
            if (agent == null)
            {
                return Results.NotFound(new ErrorResponse { Error = $"Agent not found: {agentId}" });
            }

            var raw = agent.Interaction.GetCurrentLlmInputRaw();
            return Results.Text(raw, "text/plain; charset=utf-8");
        });

        agents.MapGet("/{agentId}/apps", (
            string sessionId,
            string agentId,
            ISessionManager sessionManager) =>
        {
            var agent = ResolveAgent(sessionManager, sessionId, agentId);
            if (agent == null)
            {
                return Results.NotFound(new ErrorResponse { Error = $"Agent not found: {agentId}" });
            }

            var apps = agent.Host.GetAllApps()
                .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .Select(a => new AppSummaryResponse
                {
                    Name = a.Name,
                    Description = a.AppDescription,
                    Tags = a.Tags.ToList(),
                    IsStarted = agent.Host.IsStarted(a.Name)
                })
                .ToList();

            return Results.Ok(apps);
        });
    }

    /// <summary>
    /// 解析 Agent（先找 Session，再找 Agent）。
    /// </summary>
    private static AgentContext? ResolveAgent(
        ISessionManager sessionManager,
        string sessionId,
        string agentId)
    {
        var session = sessionManager.GetSession(sessionId);
        return session?.GetAgent(agentId);
    }
}
