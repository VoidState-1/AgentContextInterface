using ACI.Core.Abstractions;
using ACI.Core.Models;
using System.Threading;

namespace ACI.Framework.Runtime;

/// <summary>
/// 运行时上下文实现
/// </summary>
public class RuntimeContext : IContext
{
    private readonly IWindowManager _windows;
    private readonly IEventBus _events;
    private readonly ISeqClock _clock;
    private readonly IContextManager _context;
    private readonly AgentProfile _profile;
    private readonly LocalMessageChannel _messageChannel;
    private readonly IServiceProvider? _serviceProvider;

    // 窗口刷新委托（由 FrameworkHost 设置）
    private Action<string>? _refreshHandler;
    private Func<string, Func<CancellationToken, Task>, string?, string>? _startBackgroundTaskHandler;
    private Func<string, bool>? _cancelBackgroundTaskHandler;
    private Func<Func<Task>, CancellationToken, Task>? _runOnSessionHandler;

    public RuntimeContext(
        IWindowManager windows,
        IEventBus events,
        ISeqClock clock,
        IContextManager context,
        AgentProfile profile,
        LocalMessageChannel messageChannel,
        IServiceProvider? serviceProvider = null)
    {
        _windows = windows;
        _events = events;
        _clock = clock;
        _context = context;
        _profile = profile;
        _messageChannel = messageChannel;
        _serviceProvider = serviceProvider;
    }

    public IWindowManager Windows => _windows;
    public IEventBus Events => _events;
    public ISeqClock Clock => _clock;
    public IContextManager Context => _context;
    public AgentProfile Profile => _profile;
    public IMessageChannel MessageChannel => _messageChannel;

    /// <summary>
    /// 设置刷新处理器（由 FrameworkHost 调用）
    /// </summary>
    internal void SetRefreshHandler(Action<string> handler)
    {
        _refreshHandler = handler;
    }

    /// <summary>
    /// 配置后台任务处理器（由 SessionContext / AgentContext 调用）
    /// </summary>
    public void ConfigureBackgroundTaskHandlers(
        Func<string, Func<CancellationToken, Task>, string?, string> startHandler,
        Func<string, bool> cancelHandler,
        Func<Func<Task>, CancellationToken, Task> runOnSessionHandler)
    {
        _startBackgroundTaskHandler = startHandler;
        _cancelBackgroundTaskHandler = cancelHandler;
        _runOnSessionHandler = runOnSessionHandler;
    }

    /// <summary>
    /// 请求刷新窗口
    /// </summary>
    public void RequestRefresh(string windowId)
    {
        _refreshHandler?.Invoke(windowId);
    }

    /// <summary>
    /// 启动后台任务（立即返回）
    /// </summary>
    public string StartBackgroundTask(
        string windowId,
        Func<CancellationToken, Task> taskBody,
        string? taskId = null)
    {
        if (_startBackgroundTaskHandler == null)
        {
            throw new InvalidOperationException("Background task service is not configured.");
        }

        return _startBackgroundTaskHandler(windowId, taskBody, taskId);
    }

    /// <summary>
    /// 取消后台任务
    /// </summary>
    public bool CancelBackgroundTask(string taskId)
    {
        if (_cancelBackgroundTaskHandler == null)
        {
            return false;
        }

        return _cancelBackgroundTaskHandler(taskId);
    }

    /// <summary>
    /// 回到会话串行上下文执行
    /// </summary>
    public Task RunOnSessionAsync(Func<Task> action, CancellationToken ct = default)
    {
        if (_runOnSessionHandler == null)
        {
            return action();
        }

        return _runOnSessionHandler(action, ct);
    }

    public T? GetService<T>() where T : class
    {
        return _serviceProvider?.GetService(typeof(T)) as T;
    }
}
