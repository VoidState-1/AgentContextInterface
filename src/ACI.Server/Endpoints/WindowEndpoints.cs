using ACI.Server.Services;
using System.Text.Json;

namespace ACI.Server.Endpoints;

/// <summary>
/// 窗口相关端点（Agent 级）
/// </summary>
public static class WindowEndpoints
{
    public static void MapWindowEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/sessions/{sessionId}/agents/{agentId}/windows")
            .WithTags("Windows");

        // 获取所有窗口
        group.MapGet("/", (string sessionId, string agentId, ISessionManager sessionManager) =>
        {
            var agent = ResolveAgent(sessionManager, sessionId, agentId);
            if (agent == null)
            {
                return Results.NotFound(new { Error = $"Agent 不存在: {agentId}" });
            }

            var windows = agent.Windows.GetAll()
                .Select(w => new
                {
                    w.Id,
                    Description = w.Description?.Render(),
                    Content = w.Render(),
                    w.AppName,
                    w.Meta.CreatedAt,
                    w.Meta.UpdatedAt,
                    Actions = w.Actions.Select(a => new
                    {
                        a.Id,
                        a.Label,
                        ParamSchema = a.ParamsSchema
                    })
                });

            return Results.Ok(windows);
        });

        // 获取单个窗口
        group.MapGet("/{windowId}", (
            string sessionId, string agentId, string windowId,
            ISessionManager sessionManager) =>
        {
            var agent = ResolveAgent(sessionManager, sessionId, agentId);
            if (agent == null)
            {
                return Results.NotFound(new { Error = $"Agent 不存在: {agentId}" });
            }

            var window = agent.Windows.Get(windowId);
            if (window == null)
            {
                return Results.NotFound(new { Error = $"窗口不存在: {windowId}" });
            }

            return Results.Ok(new
            {
                window.Id,
                Description = window.Description?.Render(),
                Content = window.Render(),
                window.AppName,
                window.Meta.CreatedAt,
                window.Meta.UpdatedAt,
                Actions = window.Actions.Select(a => new
                {
                    a.Id,
                    a.Label,
                    ParamSchema = a.ParamsSchema
                })
            });
        });

        // 执行操作
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
                return Results.NotFound(new { Error = $"会话不存在: {sessionId}" });
            }

            if (session.GetAgent(agentId) == null)
            {
                return Results.NotFound(new { Error = $"Agent 不存在: {agentId}" });
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
    /// 解析 Agent
    /// </summary>
    private static AgentContext? ResolveAgent(
        ISessionManager sessionManager, string sessionId, string agentId)
    {
        var session = sessionManager.GetSession(sessionId);
        return session?.GetAgent(agentId);
    }
}

/// <summary>
/// 操作请求
/// </summary>
public class ActionRequest
{
    public JsonElement? Params { get; set; }
}
