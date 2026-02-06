using System.Collections.Concurrent;
using ContextUI.Core.Abstractions;
using ContextUI.LLM.Abstractions;
using ContextUI.Server.Hubs;

namespace ContextUI.Server.Services;

/// <summary>
/// 会话管理器接口
/// </summary>
public interface ISessionManager
{
    /// <summary>
    /// 创建新会话
    /// </summary>
    SessionContext CreateSession(string? sessionId = null);

    /// <summary>
    /// 获取会话
    /// </summary>
    SessionContext? GetSession(string sessionId);

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
/// 会话管理器实现
/// </summary>
public class SessionManager : ISessionManager
{
    private readonly ConcurrentDictionary<string, SessionContext> _sessions = new();
    private readonly ConcurrentDictionary<string, Action<WindowChangedEvent>> _windowHandlers = new();
    private readonly ILLMBridge _llmBridge;
    private readonly IContextUIHubNotifier _hubNotifier;
    private readonly Action<Framework.Runtime.FrameworkHost>? _configureApps;

    public event Action<SessionChangeEvent>? OnSessionChange;

    public SessionManager(
        ILLMBridge llmBridge,
        IContextUIHubNotifier hubNotifier,
        Action<Framework.Runtime.FrameworkHost>? configureApps = null)
    {
        _llmBridge = llmBridge;
        _hubNotifier = hubNotifier;
        _configureApps = configureApps;
    }

    public SessionContext CreateSession(string? sessionId = null)
    {
        sessionId ??= Guid.NewGuid().ToString("N");

        var context = new SessionContext(sessionId, _llmBridge, _configureApps);
        _sessions[sessionId] = context;
        BindWindowNotifications(context);

        OnSessionChange?.Invoke(new SessionChangeEvent(sessionId, SessionChangeType.Created));

        return context;
    }

    public SessionContext? GetSession(string sessionId)
    {
        return _sessions.GetValueOrDefault(sessionId);
    }

    public void CloseSession(string sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var context))
        {
            UnbindWindowNotifications(context);
            context.Dispose();
            OnSessionChange?.Invoke(new SessionChangeEvent(sessionId, SessionChangeType.Closed));
        }
    }

    public IEnumerable<string> GetActiveSessions()
    {
        return _sessions.Keys;
    }

    private void BindWindowNotifications(SessionContext context)
    {
        Action<WindowChangedEvent> handler = evt => _ = NotifyWindowChangeAsync(context.SessionId, evt);
        context.Windows.OnChanged += handler;
        _windowHandlers[context.SessionId] = handler;
    }

    private void UnbindWindowNotifications(SessionContext context)
    {
        if (_windowHandlers.TryRemove(context.SessionId, out var handler))
        {
            context.Windows.OnChanged -= handler;
        }
    }

    private async Task NotifyWindowChangeAsync(string sessionId, WindowChangedEvent evt)
    {
        try
        {
            if (evt.Type == WindowEventType.Created && evt.Window != null)
            {
                await _hubNotifier.NotifyWindowCreated(sessionId, evt.Window);
                return;
            }

            if (evt.Type == WindowEventType.Updated && evt.Window != null)
            {
                await _hubNotifier.NotifyWindowUpdated(sessionId, evt.Window);
                return;
            }

            if (evt.Type == WindowEventType.Removed)
            {
                await _hubNotifier.NotifyWindowClosed(sessionId, evt.WindowId);
            }
        }
        catch
        {
            // 保持会话逻辑健壮，忽略通知失败
        }
    }
}
