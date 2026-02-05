using ContextUI.Server.Services;

namespace ContextUI.Server.Endpoints;

/// <summary>
/// 会话相关端点
/// </summary>
public static class SessionEndpoints
{
    public static void MapSessionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/sessions")
            .WithTags("Sessions");

        // 获取所有会话
        group.MapGet("/", (ISessionManager sessionManager) =>
        {
            var sessions = sessionManager.GetActiveSessions()
                .Select(id => sessionManager.GetSession(id))
                .Where(s => s != null)
                .Select(s => new
                {
                    s!.SessionId,
                    s.CreatedAt
                });

            return Results.Ok(sessions);
        });

        // 创建新会话
        group.MapPost("/", (ISessionManager sessionManager) =>
        {
            var session = sessionManager.CreateSession();
            return Results.Created($"/api/sessions/{session.SessionId}", new
            {
                session.SessionId,
                session.CreatedAt
            });
        });

        // 获取单个会话
        group.MapGet("/{sessionId}", (string sessionId, ISessionManager sessionManager) =>
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
                WindowCount = session.Windows.GetAll().Count()
            });
        });

        // 关闭会话
        group.MapDelete("/{sessionId}", (string sessionId, ISessionManager sessionManager) =>
        {
            var session = sessionManager.GetSession(sessionId);
            if (session == null)
            {
                return Results.NotFound(new { Error = $"会话不存在: {sessionId}" });
            }

            sessionManager.CloseSession(sessionId);
            return Results.NoContent();
        });
    }
}
