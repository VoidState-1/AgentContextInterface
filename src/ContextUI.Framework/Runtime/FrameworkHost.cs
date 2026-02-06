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
    private readonly HashSet<string> _startedApps = [];
    private readonly Dictionary<string, string> _windowToApp = [];  // windowId -> appName
    private readonly Dictionary<string, string?> _windowIntents = [];  // windowId -> intent
    private readonly RuntimeContext _context;
    private readonly ISeqClock _clock;
    private readonly IEventBus _events;
    private readonly IWindowManager _windows;

    public FrameworkHost(RuntimeContext context)
    {
        _context = context;
        _clock = context.Clock;
        _events = context.Events;
        _windows = context.Windows;

        // 设置刷新处理器
        _context.SetRefreshHandler(RefreshWindow);
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
    /// 启动应用生命周期（不创建窗口）
    /// </summary>
    public void Start(string appName)
    {
        if (!_apps.TryGetValue(appName, out var app))
        {
            throw new InvalidOperationException($"应用 '{appName}' 未注册");
        }

        EnsureStarted(appName, app);
    }

    /// <summary>
    /// 获取已注册的应用列表
    /// </summary>
    public IEnumerable<(string Name, string? Description)> GetApps()
    {
        return _apps.Values.Select(a => (a.Name, a.AppDescription));
    }

    /// <summary>
    /// 获取全部应用实例
    /// </summary>
    public IEnumerable<ContextApp> GetAllApps()
    {
        return _apps.Values;
    }

    /// <summary>
    /// 应用是否已启动生命周期
    /// </summary>
    public bool IsStarted(string appName) => _startedApps.Contains(appName);

    /// <summary>
    /// 获取应用
    /// </summary>
    public ContextApp? GetApp(string name)
    {
        return _apps.GetValueOrDefault(name);
    }

    /// <summary>
    /// 通过窗口 ID 获取应用
    /// </summary>
    public ContextApp? GetAppByWindowId(string windowId)
    {
        if (_windowToApp.TryGetValue(windowId, out var appName))
        {
            return _apps.GetValueOrDefault(appName);
        }
        return null;
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

        EnsureStarted(appName, app);

        // 创建窗口
        var definition = app.CreateWindow(intent);
        var window = definition.ToWindow();
        window.AppName = appName;

        // 设置 seq
        var seq = _clock.Next();
        window.Meta.CreatedAt = seq;
        window.Meta.UpdatedAt = seq;

        // 记录映射
        _windowToApp[window.Id] = appName;
        _windowIntents[window.Id] = intent;

        // 发布事件
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
    /// 刷新窗口（原地更新，保持 CreatedAt 不变）
    /// </summary>
    public void RefreshWindow(string windowId)
    {
        var window = _windows.Get(windowId);
        if (window == null) return;

        var app = GetAppByWindowId(windowId);
        if (app == null) return;

        var intent = _windowIntents.GetValueOrDefault(windowId);

        // 调用应用的刷新方法获取新内容
        var newDefinition = app.RefreshWindow(windowId, intent);

        // 原地更新窗口属性
        var oldCreatedAt = window.Meta.CreatedAt;

        window.Description = newDefinition.Description;
        window.Content = newDefinition.Content;
        window.Actions.Clear();
        window.Actions.AddRange(newDefinition.Actions.Select(a => a.ToActionDefinition()));

        // 保持 CreatedAt 不变，只更新 UpdatedAt
        window.Meta.CreatedAt = oldCreatedAt;
        window.Meta.UpdatedAt = _clock.Next();

        // 更新 Handler
        window.Handler = new ContextActionHandler(newDefinition.Actions);

        if (_windows is WindowManager wm)
        {
            wm.NotifyUpdated(windowId);
        }

        // 发布窗口更新事件
        _events.Publish(new WindowRefreshedEvent(
            Seq: window.Meta.UpdatedAt,
            WindowId: windowId,
            AppName: window.AppName ?? ""
        ));
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
                _windowToApp.Remove(windowId);
                _windowIntents.Remove(windowId);
            }

            app.OnDestroy();
            _startedApps.Remove(appName);
        }
    }

    /// <summary>
    /// 确保应用已启动
    /// </summary>
    private void EnsureStarted(string appName, ContextApp app)
    {
        if (_startedApps.Contains(appName))
        {
            return;
        }

        app.Initialize(_appStates[appName], _context);
        app.OnCreate();
        _startedApps.Add(appName);
    }
}

/// <summary>
/// 窗口刷新事件
/// </summary>
public record WindowRefreshedEvent(
    int Seq,
    string WindowId,
    string AppName
) : IEvent;

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
