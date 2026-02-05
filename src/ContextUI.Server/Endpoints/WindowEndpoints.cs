using ContextUI.Core.Abstractions;
using ContextUI.Server.Services;

namespace ContextUI.Server.Endpoints;

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
                    w.Description,
                    Content = w.Render(),
                    w.AppName,
                    w.Meta.CreatedAt,
                    w.Meta.UpdatedAt
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
                window.Description,
                Content = window.Render(),
                window.AppName,
                window.Meta.CreatedAt,
                window.Meta.UpdatedAt,
                Actions = window.Actions.Select(a => new
                {
                    a.Id,
                    a.Label
                })
            });
        });

        // 执行操作
        group.MapPost("/{windowId}/actions/{actionId}", async (
            string sessionId,
            string windowId,
            string actionId,
            ActionRequest? request,
            ISessionManager sessionManager) =>
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

            // 处理关闭操作
            if (actionId == "close")
            {
                session.Windows.Remove(windowId);
                session.Context.MarkWindowObsolete(windowId);
                return Results.Ok(new { Success = true, Message = "窗口已关闭" });
            }

            if (window.Handler == null)
            {
                return Results.BadRequest(new { Error = "窗口不支持操作" });
            }

            var context = new ActionContext
            {
                Window = window,
                ActionId = actionId,
                Parameters = request?.Params
            };

            var result = await window.Handler.ExecuteAsync(context);

            // 如果需要刷新
            if (result.ShouldRefresh)
            {
                session.Host.RefreshWindow(windowId);
            }

            // 如果需要关闭
            if (result.ShouldClose)
            {
                session.Windows.Remove(windowId);
                session.Context.MarkWindowObsolete(windowId);
            }

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
    public Dictionary<string, object>? Params { get; set; }
}
