using ACI.Server.Dto;
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
                return Results.NotFound(new ErrorResponse { Error = $"Agent not found: {agentId}" });
            }

            var windows = agent.Windows.GetAll()
                .Select(ApiResponseMapper.ToWindowSummary)
                .ToList();

            return Results.Ok(windows);
        });

        group.MapGet("/{windowId}", (
            string sessionId,
            string agentId,
            string windowId,
            ISessionManager sessionManager) =>
        {
            var agent = ResolveAgent(sessionManager, sessionId, agentId);
            if (agent == null)
            {
                return Results.NotFound(new ErrorResponse { Error = $"Agent not found: {agentId}" });
            }

            var window = agent.Windows.Get(windowId);
            if (window == null)
            {
                return Results.NotFound(new ErrorResponse { Error = $"Window not found: {windowId}" });
            }

            return Results.Ok(ApiResponseMapper.ToWindowSummary(window));
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
                return Results.NotFound(new ErrorResponse { Error = $"Session not found: {sessionId}" });
            }

            if (session.GetAgent(agentId) == null)
            {
                return Results.NotFound(new ErrorResponse { Error = $"Agent not found: {agentId}" });
            }

            var result = await session.ExecuteWindowActionAsync(
                agentId,
                windowId,
                actionId,
                request?.Params,
                ct);

            return Results.Ok(new WindowActionInvokeResponse
            {
                Success = result.Success,
                Message = result.Message,
                Summary = result.Summary
            });
        });
    }

    /// <summary>
    /// 解析 Agent。
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

/// <summary>
/// 窗口 Action 请求。
/// </summary>
public class ActionRequest
{
    /// <summary>
    /// Action 参数。
    /// </summary>
    public JsonElement? Params { get; set; }
}
