using ACI.Server.Services;

namespace ACI.Server.Endpoints;

/// <summary>
/// Session persistence endpoints.
/// </summary>
public static class PersistenceEndpoints
{
    public static void MapPersistenceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/sessions")
            .WithTags("Persistence");

        group.MapPost("/{sessionId}/save", async (
            string sessionId,
            ISessionManager sessionManager,
            CancellationToken ct) =>
        {
            var saved = await sessionManager.SaveSessionAsync(sessionId, ct);
            if (!saved)
            {
                return Results.NotFound(new { Error = $"Session not found: {sessionId}" });
            }

            return Results.Ok(new
            {
                SessionId = sessionId,
                Saved = true,
                SavedAt = DateTime.UtcNow
            });
        });

        group.MapPost("/{sessionId}/load", async (
            string sessionId,
            ISessionManager sessionManager,
            CancellationToken ct) =>
        {
            Session? session;
            try
            {
                session = await sessionManager.LoadSessionAsync(sessionId, ct);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }

            if (session == null)
            {
                return Results.NotFound(new { Error = $"Saved session not found: {sessionId}" });
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
                    a.Profile.Role
                })
            });
        });

        group.MapGet("/saved", async (
            ISessionManager sessionManager,
            CancellationToken ct) =>
        {
            var sessions = await sessionManager.ListSavedSessionsAsync(ct);
            return Results.Ok(sessions);
        });

        group.MapDelete("/saved/{sessionId}", async (
            string sessionId,
            ISessionManager sessionManager,
            CancellationToken ct) =>
        {
            var deleted = await sessionManager.DeleteSavedSessionAsync(sessionId, ct);
            if (!deleted)
            {
                return Results.NotFound(new { Error = $"Saved session not found: {sessionId}" });
            }

            return Results.NoContent();
        });
    }
}
