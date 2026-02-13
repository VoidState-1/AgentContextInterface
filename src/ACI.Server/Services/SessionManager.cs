using System.Collections.Concurrent;
using ACI.Core.Abstractions;
using ACI.Framework.Runtime;
using ACI.LLM.Abstractions;
using ACI.Server.Dto;
using ACI.Server.Hubs;
using ACI.Server.Settings;

namespace ACI.Server.Services;

/// <summary>
/// 会话管理器接口（管理 Session 而非单个 Agent）
/// </summary>
public interface ISessionManager
{
    /// <summary>
    /// 创建新会话（支持多 Agent）
    /// </summary>
    Session CreateSession(CreateSessionRequest? request = null);

    /// <summary>
    /// 获取会话
    /// </summary>
    Session? GetSession(string sessionId);

    /// <summary>
    /// 关闭会话
    /// </summary>
    void CloseSession(string sessionId);

    /// <summary>
    /// 获取所有活动会话 ID
    /// </summary>
    IEnumerable<string> GetActiveSessions();

    /// <summary>
    /// 会话变更事件
    /// </summary>
    event Action<SessionChangeEvent>? OnSessionChange;
}

/// <summary>
/// 会话变更事件
/// </summary>
public record SessionChangeEvent(string SessionId, SessionChangeType Type);

/// <summary>
/// 会话变更类型
/// </summary>
public enum SessionChangeType
{
    Created,
    Closed
}

/// <summary>
/// 会话管理器实现（管理 Session）
/// </summary>
public class SessionManager : ISessionManager
{
    private readonly ConcurrentDictionary<string, Session> _sessions = new();
    private readonly ConcurrentDictionary<string, List<(string AgentId, Action<WindowChangedEvent> Handler)>> _windowHandlers = new();
    private readonly ILLMBridge _llmBridge;
    private readonly IACIHubNotifier _hubNotifier;
    private readonly ACIOptions _options;
    private readonly Action<FrameworkHost>? _configureApps;

    public event Action<SessionChangeEvent>? OnSessionChange;

    public SessionManager(
        ILLMBridge llmBridge,
        IACIHubNotifier hubNotifier,
        ACIOptions options,
        Action<FrameworkHost>? configureApps = null)
    {
        _llmBridge = llmBridge;
        _hubNotifier = hubNotifier;
        _options = options;
        _configureApps = configureApps;
    }

    public Session CreateSession(CreateSessionRequest? request = null)
    {
        var sessionId = Guid.NewGuid().ToString("N");

        // 解析 Agent 配置（为空则默认单 Agent）
        var profiles = ParseProfiles(request);

        var session = new Session(
            sessionId, profiles, _llmBridge, _options, _configureApps);
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
            UnbindWindowNotifications(session);
            session.Dispose();
            OnSessionChange?.Invoke(new SessionChangeEvent(sessionId, SessionChangeType.Closed));
        }
    }

    public IEnumerable<string> GetActiveSessions()
    {
        return _sessions.Keys;
    }

    /// <summary>
    /// 解析请求中的 Agent 配置。
    /// </summary>
    private static IReadOnlyList<AgentProfile> ParseProfiles(CreateSessionRequest? request)
    {
        if (request?.Agents == null || request.Agents.Count == 0)
        {
            return [AgentProfile.Default()];
        }

        return request.Agents.Select(a => a.ToProfile()).ToList();
    }

    /// <summary>
    /// 为 Session 中每个 Agent 绑定窗口通知。
    /// </summary>
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

    /// <summary>
    /// 解绑窗口通知。
    /// </summary>
    private void UnbindWindowNotifications(Session session)
    {
        if (!_windowHandlers.TryRemove(session.SessionId, out var handlers)) return;

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
            // 保持会话逻辑健壮，忽略通知失败
        }
    }
}
