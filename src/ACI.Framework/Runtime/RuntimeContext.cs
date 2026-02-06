using ACI.Core.Abstractions;
using ACI.Core.Models;
using ACI.Core.Services;

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
    private readonly IServiceProvider? _serviceProvider;

    // 窗口刷新委托（由 FrameworkHost 设置）
    private Action<string>? _refreshHandler;

    public RuntimeContext(
        IWindowManager windows,
        IEventBus events,
        ISeqClock clock,
        IContextManager context,
        IServiceProvider? serviceProvider = null)
    {
        _windows = windows;
        _events = events;
        _clock = clock;
        _context = context;
        _serviceProvider = serviceProvider;
    }

    public IWindowManager Windows => _windows;
    public IEventBus Events => _events;
    public ISeqClock Clock => _clock;
    public IContextManager Context => _context;

    /// <summary>
    /// 设置刷新处理器（由 FrameworkHost 调用）
    /// </summary>
    internal void SetRefreshHandler(Action<string> handler)
    {
        _refreshHandler = handler;
    }

    /// <summary>
    /// 请求刷新窗口
    /// </summary>
    public void RequestRefresh(string windowId)
    {
        _refreshHandler?.Invoke(windowId);
    }

    public T? GetService<T>() where T : class
    {
        return _serviceProvider?.GetService(typeof(T)) as T;
    }
}
