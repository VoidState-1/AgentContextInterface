using ACI.Server.Services;
using System.Text.Json;

namespace ACI.Server.Endpoints;

/// <summary>
/// 窗口相关端点
/// </summary>
public static class WindowEndpoints
{
    public static void MapWindowEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/sessions/{sessionId}/windows")
            .WithTags("Windows");

        // 获取所有窗口
        group.MapGet("/", (string sessionId, ISessionManager sessionManager) =>
        {
            var session = sessionManager.GetSession(sessionId);
            if (session == null)
            {
                return Results.NotFound(new { Error = $"会话不存在: {sessionId}" });
            }

            var windows = session.Windows.GetAll()
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
                        Parameters = a.Parameters.Select(p => new
                        {
                            p.Name,
                            p.Type,
                            p.Required,
                            p.Default
                        })
                    })
                });

            return Results.Ok(windows);
        });

        // 获取单个窗口
        group.MapGet("/{windowId}", (string sessionId, string windowId, ISessionManager sessionManager) =>
        {
            var session = sessionManager.GetSession(sessionId);
            if (session == null)
            {
                return Results.NotFound(new { Error = $"会话不存在: {sessionId}" });
            }

            var window = session.Windows.Get(windowId);
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
                    Parameters = a.Parameters.Select(p => new
                    {
                        p.Name,
                        p.Type,
                        p.Required,
                        p.Default
                    })
                })
            });
        });

        // 执行操作
        group.MapPost("/{windowId}/actions/{actionId}", async (
            string sessionId,
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

            var result = await session.RunSerializedAsync(
                () => session.Interaction.ExecuteWindowActionAsync(windowId, actionId, request?.Params),
                ct);

            return Results.Ok(new
            {
                result.Success,
                result.Message,
                result.Summary
            });
        });
    }
}

/// <summary>
/// 操作请求
/// </summary>
public class ActionRequest
{
    public JsonElement? Params { get; set; }
}
