using System.Collections.Concurrent;
using ContextUI.LLM.Abstractions;

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
    private readonly ILLMBridge _llmBridge;
    private readonly Action<Framework.Runtime.FrameworkHost>? _configureApps;

    public event Action<SessionChangeEvent>? OnSessionChange;

    public SessionManager(ILLMBridge llmBridge, Action<Framework.Runtime.FrameworkHost>? configureApps = null)
    {
        _llmBridge = llmBridge;
        _configureApps = configureApps;
    }

    public SessionContext CreateSession(string? sessionId = null)
    {
        sessionId ??= Guid.NewGuid().ToString("N");

        var context = new SessionContext(sessionId, _llmBridge, _configureApps);
        _sessions[sessionId] = context;

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
            context.Dispose();
            OnSessionChange?.Invoke(new SessionChangeEvent(sessionId, SessionChangeType.Closed));
        }
    }

    public IEnumerable<string> GetActiveSessions()
    {
        return _sessions.Keys;
    }
}
