using ACI.Server.Services;
using System.Text.Json;

namespace ACI.Server.Endpoints;

/// <summary>
/// 窗口相关端点（Agent 级别）。
/// </summary>
public static class WindowEndpoints
{
    /// <summary>
    /// 注册窗口 API。
    /// </summary>
    public static void MapWindowEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/sessions/{sessionId}/agents/{agentId}/windows")
            .WithTags("Windows");

        group.MapGet("/", (string sessionId, string agentId, ISessionManager sessionManager) =>
        {
            var agent = ResolveAgent(sessionManager, sessionId, agentId);
            if (agent == null)
            {
                return Results.NotFound(new { Error = $"Agent not found: {agentId}" });
            }

            var windows = agent.Windows.GetAll()
                .Select(w => new
                {
                    w.Id,
                    Description = w.Description?.Render(),
                    Content = w.Render(),
                    Namespaces = w.NamespaceRefs,
                    w.AppName,
                    w.Meta.CreatedAt,
                    w.Meta.UpdatedAt
                });

            return Results.Ok(windows);
        });

        group.MapGet("/{windowId}", (
            string sessionId, string agentId, string windowId,
            ISessionManager sessionManager) =>
        {
            var agent = ResolveAgent(sessionManager, sessionId, agentId);
            if (agent == null)
            {
                return Results.NotFound(new { Error = $"Agent not found: {agentId}" });
            }

            var window = agent.Windows.Get(windowId);
            if (window == null)
            {
                return Results.NotFound(new { Error = $"Window not found: {windowId}" });
            }

            return Results.Ok(new
            {
                window.Id,
                Description = window.Description?.Render(),
                Content = window.Render(),
                Namespaces = window.NamespaceRefs,
                window.AppName,
                window.Meta.CreatedAt,
                window.Meta.UpdatedAt
            });
        });

        group.MapPost("/{windowId}/actions/{actionId}", async (
            string sessionId,
            string agentId,
            string windowId,
            string actionId,
            ActionRequest? request,
            ISessionManager sessionManager,
            CancellationToken ct) =>
        {
            var session = sessionManager.GetSession(sessionId);
            if (session == null)
            {
                return Results.NotFound(new { Error = $"Session not found: {sessionId}" });
            }

            if (session.GetAgent(agentId) == null)
            {
                return Results.NotFound(new { Error = $"Agent not found: {agentId}" });
            }

            var result = await session.ExecuteWindowActionAsync(
                agentId,
                windowId,
                actionId,
                request?.Params,
                ct);

            return Results.Ok(new
            {
                result.Success,
                result.Message,
                result.Summary
            });
        });
    }

    /// <summary>
    /// 解析 Agent。
    /// </summary>
    private static AgentContext? ResolveAgent(
        ISessionManager sessionManager, string sessionId, string agentId)
    {
        var session = sessionManager.GetSession(sessionId);
        return session?.GetAgent(agentId);
    }
}

/// <summary>
/// 动作请求。
/// </summary>
public class ActionRequest
{
    /// <summary>
    /// 工具参数。
    /// </summary>
    public JsonElement? Params { get; set; }
}
