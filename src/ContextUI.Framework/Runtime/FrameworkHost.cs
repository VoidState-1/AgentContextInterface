using ContextUI.Core.Abstractions;
using ContextUI.Core.Models;
using ContextUI.Core.Services;

namespace ContextUI.Framework.Runtime;

/// <summary>
/// 框架宿主 - 管理应用生命周期和注册
/// </summary>
public class FrameworkHost
{
    private readonly Dictionary<string, ContextApp> _apps = [];
    private readonly Dictionary<string, InMemoryAppState> _appStates = [];
    private readonly IContext _context;
    private readonly ISeqClock _clock;
    private readonly IEventBus _events;
    private readonly IWindowManager _windows;

    public FrameworkHost(IContext context)
    {
        _context = context;
        _clock = context.Clock;
        _events = context.Events;
        _windows = context.Windows;
    }

    /// <summary>
    /// 注册应用
    /// </summary>
    public void Register(ContextApp app)
    {
        _apps[app.Name] = app;
        _appStates[app.Name] = new InMemoryAppState();
    }

    /// <summary>
    /// 获取已注册的应用列表
    /// </summary>
    public IEnumerable<(string Name, string? Description)> GetApps()
    {
        return _apps.Values.Select(a => (a.Name, a.AppDescription));
    }

    /// <summary>
    /// 获取应用
    /// </summary>
    public ContextApp? GetApp(string name)
    {
        return _apps.GetValueOrDefault(name);
    }

    /// <summary>
    /// 启动应用
    /// </summary>
    public Window Launch(string appName, string? intent = null)
    {
        if (!_apps.TryGetValue(appName, out var app))
        {
            throw new InvalidOperationException($"应用 '{appName}' 未注册");
        }

        // 注入状态和上下文
        app.Initialize(_appStates[appName], _context);

        // 生命周期
        app.OnCreate();

        // 创建窗口
        var definition = app.CreateWindow(intent);
        var window = definition.ToWindow();
        window.AppName = appName;

        // 发布事件
        var seq = _clock.Next();
        _events.Publish(new AppCreatedEvent(
            Seq: seq,
            AppName: appName,
            Target: intent,
            Success: true
        ));

        // 添加到窗口管理器
        _windows.Add(window);

        return window;
    }

    /// <summary>
    /// 关闭应用
    /// </summary>
    public void Close(string appName)
    {
        if (_apps.TryGetValue(appName, out var app))
        {
            // 移除该应用管理的所有窗口
            foreach (var windowId in app.ManagedWindowIds.ToList())
            {
                _windows.Remove(windowId);
            }

            app.OnDestroy();
        }
    }
}

/// <summary>
/// 内存状态存储实现
/// </summary>
public class InMemoryAppState : IAppState
{
    private readonly Dictionary<string, object> _data = [];

    public void Set<T>(string key, T value)
    {
        _data[key] = value!;
    }

    public T? Get<T>(string key)
    {
        return _data.TryGetValue(key, out var value) ? (T)value : default;
    }

    public bool Has(string key) => _data.ContainsKey(key);

    public void Clear() => _data.Clear();
}
