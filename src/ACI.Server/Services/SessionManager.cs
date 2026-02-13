using System.Collections.Concurrent;
using ACI.Core.Abstractions;
using ACI.Framework.Runtime;
using ACI.LLM.Abstractions;
using ACI.Server.Hubs;
using ACI.Server.Settings;

namespace ACI.Server.Services;

/// <summary>
/// 会话管理器接口
/// </summary>
public interface ISessionManager
{
    /// <summary>
    /// 创建新会话（当前使用默认单 Agent 模式，步骤 6 将改造为多 Agent）
    /// </summary>
    AgentContext CreateSession(string? sessionId = null);

    /// <summary>
    /// 获取会话
    /// </summary>
    AgentContext? GetSession(string sessionId);

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
/// 会话管理器实现（当前使用 AgentContext 作为过渡，步骤 6 将改为管理 Session）
/// </summary>
public class SessionManager : ISessionManager
{
    private readonly ConcurrentDictionary<string, AgentContext> _sessions = new();
    private readonly ConcurrentDictionary<string, Action<WindowChangedEvent>> _windowHandlers = new();
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

    public AgentContext CreateSession(string? sessionId = null)
    {
        sessionId ??= Guid.NewGuid().ToString("N");

        // 当前过渡期：使用默认 Profile，单 Agent 模式
        var profile = new AgentProfile { Id = sessionId, Name = "Default Agent" };
        var context = new AgentContext(profile, _llmBridge, _options, configureApps: _configureApps);
        _sessions[sessionId] = context;
        BindWindowNotifications(context);

        OnSessionChange?.Invoke(new SessionChangeEvent(sessionId, SessionChangeType.Created));

        return context;
    }

    public AgentContext? GetSession(string sessionId)
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

    private void BindWindowNotifications(AgentContext context)
    {
        Action<WindowChangedEvent> handler = evt => _ = NotifyWindowChangeAsync(context.AgentId, evt);
        context.Windows.OnChanged += handler;
        _windowHandlers[context.AgentId] = handler;
    }

    private void UnbindWindowNotifications(AgentContext context)
    {
        if (_windowHandlers.TryRemove(context.AgentId, out var handler))
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
