using System.Threading;
using ACI.Core.Abstractions;
using ACI.Core.Models;

namespace ACI.Framework.Runtime;

/// <summary>
/// 应用运行时上下文实现。
/// </summary>
public class RuntimeContext : IContext
{
    /// <summary>
    /// 核心依赖。
    /// </summary>
    private readonly IWindowManager _windows;
    private readonly IEventBus _events;
    private readonly ISeqClock _clock;
    private readonly IContextManager _context;
    private readonly IToolNamespaceRegistry _toolNamespaces;
    private readonly AgentProfile _profile;
    private readonly LocalMessageChannel _messageChannel;
    private readonly IServiceProvider? _serviceProvider;

    /// <summary>
    /// 回调处理器。
    /// </summary>
    private Action<string>? _refreshHandler;
    private Func<string, Func<CancellationToken, Task>, string?, string>? _startBackgroundTaskHandler;
    private Func<string, bool>? _cancelBackgroundTaskHandler;
    private Func<Func<Task>, CancellationToken, Task>? _runOnSessionHandler;

    /// <summary>
    /// 构造运行时上下文。
    /// </summary>
    public RuntimeContext(
        IWindowManager windows,
        IEventBus events,
        ISeqClock clock,
        IContextManager context,
        IToolNamespaceRegistry toolNamespaces,
        AgentProfile profile,
        LocalMessageChannel messageChannel,
        IServiceProvider? serviceProvider = null)
    {
        _windows = windows;
        _events = events;
        _clock = clock;
        _context = context;
        _toolNamespaces = toolNamespaces;
        _profile = profile;
        _messageChannel = messageChannel;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// 窗口管理器。
    /// </summary>
    public IWindowManager Windows => _windows;

    /// <summary>
    /// 事件总线。
    /// </summary>
    public IEventBus Events => _events;

    /// <summary>
    /// 时钟服务。
    /// </summary>
    public ISeqClock Clock => _clock;

    /// <summary>
    /// 上下文管理器。
    /// </summary>
    public IContextManager Context => _context;

    /// <summary>
    /// 工具命名空间注册表。
    /// </summary>
    public IToolNamespaceRegistry ToolNamespaces => _toolNamespaces;

    /// <summary>
    /// 当前 Agent 配置。
    /// </summary>
    public AgentProfile Profile => _profile;

    /// <summary>
    /// 应用间消息通道。
    /// </summary>
    public IMessageChannel MessageChannel => _messageChannel;

    /// <summary>
    /// 设置窗口刷新回调（由 FrameworkHost 注入）。
    /// </summary>
    internal void SetRefreshHandler(Action<string> handler)
    {
        _refreshHandler = handler;
    }

    /// <summary>
    /// 配置后台任务相关处理器。
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
    /// 请求刷新窗口。
    /// </summary>
    public void RequestRefresh(string windowId)
    {
        _refreshHandler?.Invoke(windowId);
    }

    /// <summary>
    /// 启动后台任务。
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
    /// 取消后台任务。
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
    /// 在会话串行上下文中执行动作。
    /// </summary>
    public Task RunOnSessionAsync(Func<Task> action, CancellationToken ct = default)
    {
        if (_runOnSessionHandler == null)
        {
            return action();
        }

        return _runOnSessionHandler(action, ct);
    }

    /// <summary>
    /// 获取服务。
    /// </summary>
    public T? GetService<T>() where T : class
    {
        return _serviceProvider?.GetService(typeof(T)) as T;
    }
}
