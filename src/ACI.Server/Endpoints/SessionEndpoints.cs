using ACI.Server.Services;
using System.Text;

namespace ACI.Server.Endpoints;

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
                    s!.AgentId,
                    s.CreatedAt
                });

            return Results.Ok(sessions);
        });

        // 创建新会话
        group.MapPost("/", (ISessionManager sessionManager) =>
        {
            var session = sessionManager.CreateSession();
            return Results.Created($"/api/sessions/{session.AgentId}", new
            {
                session.AgentId,
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
                session.AgentId,
                session.CreatedAt,
                WindowCount = session.Windows.GetAll().Count()
            });
        });

        // 获取会话上下文时间线（按 Seq 顺序）
        group.MapGet("/{sessionId}/context", (
            string sessionId,
            bool includeObsolete,
            ISessionManager sessionManager) =>
        {
            var session = sessionManager.GetSession(sessionId);
            if (session == null)
            {
                return Results.NotFound(new { Error = $"会话不存在: {sessionId}" });
            }

            var items = includeObsolete
                ? session.Context.GetArchive()
                : session.Context.GetActive();

            var timeline = items.Select(item =>
            {
                var isWindow = item.Type == Core.Models.ContextItemType.Window;
                var window = isWindow ? session.Windows.Get(item.Content) : null;

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
                        // Full XML that LLM receives for window items
                        Rendered = window?.Render(),
                        // Readable content-only text for debug UI
                        Display = window?.Content.Render(),
                        IsCompact = window?.Options.RenderMode == Core.Models.RenderMode.Compact,
                        CreatedAt = window?.Meta.CreatedAt,
                        UpdatedAt = window?.Meta.UpdatedAt
                    } : null
                };
            });

            return Results.Ok(timeline);
        });

        // 获取会话原始上下文文本（单块字符串）
        group.MapGet("/{sessionId}/context/raw", (
            string sessionId,
            bool includeObsolete,
            ISessionManager sessionManager) =>
        {
            var session = sessionManager.GetSession(sessionId);
            if (session == null)
            {
                return Results.NotFound(new { Error = $"会话不存在: {sessionId}" });
            }

            var items = includeObsolete
                ? session.Context.GetArchive()
                : session.Context.GetActive();

            var sb = new StringBuilder();
            foreach (var item in items)
            {
                if (item.Type == Core.Models.ContextItemType.Window)
                {
                    var window = session.Windows.Get(item.Content);
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

        // 获取当前发送给 LLM 的原始输入快照（单块字符串）
        group.MapGet("/{sessionId}/llm-input/raw", (
            string sessionId,
            ISessionManager sessionManager) =>
        {
            var session = sessionManager.GetSession(sessionId);
            if (session == null)
            {
                return Results.NotFound(new { Error = $"会话不存在: {sessionId}" });
            }

            var raw = session.Interaction.GetCurrentLlmInputRaw();
            return Results.Text(raw, "text/plain; charset=utf-8");
        });

        // 获取会话可用应用列表（用于调试模拟器）
        group.MapGet("/{sessionId}/apps", (string sessionId, ISessionManager sessionManager) =>
        {
            var session = sessionManager.GetSession(sessionId);
            if (session == null)
            {
                return Results.NotFound(new { Error = $"会话不存在: {sessionId}" });
            }

            var apps = session.Host.GetAllApps()
                .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .Select(app => new
                {
                    app.Name,
                    Description = app.AppDescription,
                    app.Tags,
                    IsStarted = session.Host.IsStarted(app.Name)
                });

            return Results.Ok(apps);
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
