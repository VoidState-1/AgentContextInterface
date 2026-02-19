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

        // 列出当前内存中的活跃会话（用于前端会话导航）。
        sessions.MapGet("/", (ISessionManager sessionManager) =>
        {
            var list = sessionManager.GetActiveSessions()
                .Select(id => sessionManager.GetSession(id))
                .Where(s => s != null)
                .Select(s => ApiResponseMapper.ToSessionSummary(s!))
                .ToList();

            return Results.Ok(list);
        });

        // 创建新会话，可选携带多 Agent 配置。
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

        // 获取单个会话的摘要信息（元数据，不包含上下文正文）。
        sessions.MapGet("/{sessionId}", (string sessionId, ISessionManager sessionManager) =>
        {
            var session = sessionManager.GetSession(sessionId);
            if (session == null)
            {
                return Results.NotFound(new ErrorResponse { Error = $"Session not found: {sessionId}" });
            }

            return Results.Ok(ApiResponseMapper.ToSessionSummary(session));
        });

        // 关闭并释放会话资源。
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

        // 列出会话下所有 Agent 的摘要信息。
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

        // 获取结构化上下文时间线（调试/分析用）。
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

        // 获取当前轮发送给 LLM 的最终输入文本（用于定位模型行为）。
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

        // 列出当前 Agent 可用应用及启动状态。
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
