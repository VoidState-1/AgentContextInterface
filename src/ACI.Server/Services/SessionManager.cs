using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using ACI.Core.Abstractions;
using ACI.Framework.Runtime;
using ACI.LLM.Abstractions;
using ACI.Server.Dto;
using ACI.Server.Hubs;
using ACI.Server.Settings;
using ACI.Storage;

namespace ACI.Server.Services;

public interface ISessionManager
{
    Session CreateSession(CreateSessionRequest? request = null);
    Session? GetSession(string sessionId);
    void CloseSession(string sessionId);
    IEnumerable<string> GetActiveSessions();

    Task<bool> SaveSessionAsync(string sessionId, CancellationToken ct = default);
    Task<Session?> LoadSessionAsync(string sessionId, CancellationToken ct = default);
    Task<IReadOnlyList<SessionSummary>> ListSavedSessionsAsync(CancellationToken ct = default);
    Task<bool> DeleteSavedSessionAsync(string sessionId, CancellationToken ct = default);
    void RequestAutoSave(string sessionId);

    event Action<SessionChangeEvent>? OnSessionChange;
}

public record SessionChangeEvent(string SessionId, SessionChangeType Type);

public enum SessionChangeType
{
    Created,
    Closed
}

public class SessionManager : ISessionManager
{
    private static readonly Regex AgentIdRegex = new(
        "^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$",
        RegexOptions.Compiled);

    private readonly ConcurrentDictionary<string, Session> _sessions = new();
    private readonly ConcurrentDictionary<string, List<(string AgentId, Action<WindowChangedEvent> Handler)>> _windowHandlers = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _autoSaveTokens = new();
    private readonly ILLMBridge _llmBridge;
    private readonly IACIHubNotifier _hubNotifier;
    private readonly ISessionStore _store;
    private readonly ACIOptions _options;
    private readonly Action<FrameworkHost>? _configureApps;

    public event Action<SessionChangeEvent>? OnSessionChange;

    public SessionManager(
        ILLMBridge llmBridge,
        IACIHubNotifier hubNotifier,
        ISessionStore store,
        ACIOptions options,
        Action<FrameworkHost>? configureApps = null)
    {
        _llmBridge = llmBridge;
        _hubNotifier = hubNotifier;
        _store = store;
        _options = options;
        _configureApps = configureApps;
    }

    public Session CreateSession(CreateSessionRequest? request = null)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var profiles = ParseProfiles(request);

        var session = new Session(
            sessionId,
            profiles,
            _llmBridge,
            _options,
            _configureApps,
            RequestAutoSave);
        _sessions[sessionId] = session;
        BindWindowNotifications(session);
        OnSessionChange?.Invoke(new SessionChangeEvent(sessionId, SessionChangeType.Created));

        return session;
    }

    public Session? GetSession(string sessionId)
    {
        return _sessions.GetValueOrDefault(sessionId);
    }

    public void CloseSession(string sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            CancelPendingAutoSave(sessionId);
            UnbindWindowNotifications(session);
            session.Dispose();
            OnSessionChange?.Invoke(new SessionChangeEvent(sessionId, SessionChangeType.Closed));
        }
    }

    public IEnumerable<string> GetActiveSessions()
    {
        return _sessions.Keys;
    }

    public async Task<bool> SaveSessionAsync(string sessionId, CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return false;
        }

        await _store.SaveAsync(session.TakeSnapshot(), ct);
        return true;
    }

    public async Task<Session?> LoadSessionAsync(string sessionId, CancellationToken ct = default)
    {
        var snapshot = await _store.LoadAsync(sessionId, ct);
        if (snapshot == null)
        {
            return null;
        }

        if (snapshot.Version != Session.SnapshotVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported snapshot version '{snapshot.Version}'. Current version is '{Session.SnapshotVersion}'.");
        }

        var profiles = snapshot.Agents.Select(a => a.Profile.ToProfile()).ToList();
        ValidateProfiles(profiles);

        if (_sessions.TryRemove(sessionId, out var existing))
        {
            CancelPendingAutoSave(sessionId);
            UnbindWindowNotifications(existing);
            existing.Dispose();
            OnSessionChange?.Invoke(new SessionChangeEvent(sessionId, SessionChangeType.Closed));
        }

        var restored = new Session(
            snapshot.SessionId,
            profiles,
            _llmBridge,
            _options,
            _configureApps,
            RequestAutoSave,
            snapshot.CreatedAt);
        restored.RestoreFromSnapshot(snapshot);

        _sessions[restored.SessionId] = restored;
        BindWindowNotifications(restored);
        OnSessionChange?.Invoke(new SessionChangeEvent(restored.SessionId, SessionChangeType.Created));
        return restored;
    }

    public Task<IReadOnlyList<SessionSummary>> ListSavedSessionsAsync(CancellationToken ct = default)
    {
        return _store.ListAsync(ct);
    }

    public async Task<bool> DeleteSavedSessionAsync(string sessionId, CancellationToken ct = default)
    {
        if (!await _store.ExistsAsync(sessionId, ct))
        {
            return false;
        }

        await _store.DeleteAsync(sessionId, ct);
        return true;
    }

    public void RequestAutoSave(string sessionId)
    {
        if (!_options.Persistence.AutoSave.Enabled || !_sessions.ContainsKey(sessionId))
        {
            return;
        }

        var delayMs = Math.Max(0, _options.Persistence.AutoSave.DebounceMilliseconds);
        var nextCts = new CancellationTokenSource();

        _autoSaveTokens.AddOrUpdate(
            sessionId,
            nextCts,
            (_, previous) =>
            {
                try { previous.Cancel(); } catch { /* ignore */ }
                previous.Dispose();
                return nextCts;
            });

        _ = ScheduleAutoSaveAsync(sessionId, delayMs, nextCts);
    }

    private static IReadOnlyList<AgentProfile> ParseProfiles(CreateSessionRequest? request)
    {
        if (request?.Agents == null || request.Agents.Count == 0)
        {
            return [AgentProfile.Default()];
        }

        var profiles = request.Agents.Select(a => a.ToProfile()).ToList();
        ValidateProfiles(profiles);
        return profiles;
    }

    private static void ValidateProfiles(IReadOnlyList<AgentProfile> profiles)
    {
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var profile in profiles)
        {
            if (string.IsNullOrWhiteSpace(profile.Id))
            {
                throw new ArgumentException("Agent id cannot be empty.");
            }

            if (!AgentIdRegex.IsMatch(profile.Id))
            {
                throw new ArgumentException(
                    $"Invalid agent id '{profile.Id}'. Use letters, numbers, '_' or '-', length 1-64.");
            }

            if (!seenIds.Add(profile.Id))
            {
                throw new ArgumentException($"Duplicate agent id '{profile.Id}'.");
            }

            if (string.IsNullOrWhiteSpace(profile.Name))
            {
                throw new ArgumentException($"Agent '{profile.Id}' name cannot be empty.");
            }
        }
    }

    private void BindWindowNotifications(Session session)
    {
        var handlers = new List<(string AgentId, Action<WindowChangedEvent> Handler)>();

        foreach (var agent in session.GetAllAgents())
        {
            Action<WindowChangedEvent> handler = evt =>
                _ = NotifyWindowChangeAsync(session.SessionId, agent.AgentId, evt);
            agent.Windows.OnChanged += handler;
            handlers.Add((agent.AgentId, handler));
        }

        _windowHandlers[session.SessionId] = handlers;
    }

    private void UnbindWindowNotifications(Session session)
    {
        if (!_windowHandlers.TryRemove(session.SessionId, out var handlers))
        {
            return;
        }

        foreach (var (agentId, handler) in handlers)
        {
            var agent = session.GetAgent(agentId);
            if (agent != null)
            {
                agent.Windows.OnChanged -= handler;
            }
        }
    }

    private async Task NotifyWindowChangeAsync(
        string sessionId, string agentId, WindowChangedEvent evt)
    {
        try
        {
            if (evt.Type == WindowEventType.Created && evt.Window != null)
            {
                await _hubNotifier.NotifyWindowCreated(sessionId, agentId, evt.Window);
                return;
            }

            if (evt.Type == WindowEventType.Updated && evt.Window != null)
            {
                await _hubNotifier.NotifyWindowUpdated(sessionId, agentId, evt.Window);
                return;
            }

            if (evt.Type == WindowEventType.Removed)
            {
                await _hubNotifier.NotifyWindowClosed(sessionId, agentId, evt.WindowId);
            }
        }
        catch
        {
            // Keep session logic stable even when hub notifications fail.
        }
    }

    private async Task ScheduleAutoSaveAsync(
        string sessionId,
        int delayMs,
        CancellationTokenSource cts)
    {
        try
        {
            if (delayMs > 0)
            {
                await Task.Delay(delayMs, cts.Token);
            }

            await SaveSessionAsync(sessionId, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // 旧请求被新请求覆盖，属于正常防抖行为。
        }
        catch (Exception ex)
        {
            Trace.TraceWarning(
                $"Auto save failed for session '{sessionId}': {ex.Message}");
        }
        finally
        {
            if (_autoSaveTokens.TryGetValue(sessionId, out var current) && ReferenceEquals(current, cts))
            {
                _autoSaveTokens.TryRemove(sessionId, out _);
            }

            cts.Dispose();
        }
    }

    private void CancelPendingAutoSave(string sessionId)
    {
        if (!_autoSaveTokens.TryRemove(sessionId, out var cts))
        {
            return;
        }

        try { cts.Cancel(); } catch { /* ignore */ }
        cts.Dispose();
    }
}
