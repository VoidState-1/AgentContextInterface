using ACI.Core.Abstractions;
using ACI.Core.Models;
using ACI.Core.Services;

namespace ACI.Framework.Runtime;

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

    // ========== 快照支持 ==========

    /// <summary>
    /// 采集所有已注册应用的快照。
    /// 遍历 _apps，对每个已启动的 App 调用 OnSaveState → Export，
    /// 收集 managed window IDs 和 intent 映射。
    /// </summary>
    internal List<AppSnapshot> TakeAppSnapshots()
    {
        var snapshots = new List<AppSnapshot>();

        foreach (var (appName, app) in _apps)
        {
            var isStarted = _startedApps.Contains(appName);

            if (isStarted)
            {
                app.OnSaveState();
            }

            var snapshot = new AppSnapshot
            {
                Name = appName,
                IsStarted = isStarted,
                ManagedWindowIds = isStarted
                    ? app.GetManagedWindowIdsInternal().ToList()
                    : [],
                WindowIntents = [],
                StateData = isStarted
                    ? new Dictionary<string, System.Text.Json.JsonElement>(_appStates[appName].Export())
                    : []
            };

            // 收集 intent 映射
            foreach (var windowId in snapshot.ManagedWindowIds)
            {
                if (_windowIntents.TryGetValue(windowId, out var intent))
                {
                    snapshot.WindowIntents[windowId] = intent;
                }
            }

            snapshots.Add(snapshot);
        }

        return snapshots;
    }

    /// <summary>
    /// 从快照恢复所有应用状态。
    /// 仅恢复已注册的 App（快照里有但当前未注册的 App 会被跳过）。
    /// </summary>
    internal void RestoreAppSnapshots(IReadOnlyList<AppSnapshot> appSnapshots)
    {
        foreach (var snapshot in appSnapshots)
        {
            try
            {
                if (!_apps.TryGetValue(snapshot.Name, out var app)) continue;

                // 确保 AppState 存在
                if (!_appStates.ContainsKey(snapshot.Name))
                {
                    _appStates[snapshot.Name] = new InMemoryAppState();
                }

                // 导入状态数据
                _appStates[snapshot.Name].Import(snapshot.StateData);

                if (!snapshot.IsStarted) continue;

                // 重新启动生命周期
                if (!_startedApps.Contains(snapshot.Name))
                {
                    app.Initialize(_appStates[snapshot.Name], _context);
                    _startedApps.Add(snapshot.Name);
                }

                // 恢复 managed window ID 列表
                app.RestoreManagedWindowIds(snapshot.ManagedWindowIds);

                // 恢复 intent 映射
                foreach (var (windowId, intent) in snapshot.WindowIntents)
                {
                    _windowToApp[windowId] = snapshot.Name;
                    _windowIntents[windowId] = intent;
                }

                // 调用应用的恢复回调（重建内部状态，可能创建窗口）
                app.OnRestoreState();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning(
                    $"App '{snapshot.Name}' restore failed and was skipped: {ex.Message}");
            }
        }
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
/// 内存状态存储实现（支持 Export/Import 持久化）
/// </summary>
public class InMemoryAppState : IAppState
{
    private readonly Dictionary<string, object> _data = [];

    private static readonly System.Text.Json.JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public void Set<T>(string key, T value)
    {
        _data[key] = value!;
    }

    public T? Get<T>(string key)
    {
        if (!_data.TryGetValue(key, out var value)) return default;

        // 如果值已经是目标类型，直接返回
        if (value is T typed) return typed;

        // 如果值是 JsonElement（从 Import 导入），反序列化为目标类型
        if (value is System.Text.Json.JsonElement jsonElement)
        {
            try
            {
                var deserialized = System.Text.Json.JsonSerializer.Deserialize<T>(
                    jsonElement.GetRawText(), _jsonOptions);
                if (deserialized != null)
                {
                    // 缓存反序列化结果，下次直接使用
                    _data[key] = deserialized;
                }
                return deserialized;
            }
            catch
            {
                return default;
            }
        }

        // 尝试强制转换
        try { return (T)value; }
        catch { return default; }
    }

    public bool Has(string key) => _data.ContainsKey(key);

    public void Clear() => _data.Clear();

    /// <summary>
    /// 导出所有状态为 JsonElement 字典。
    /// </summary>
    public IReadOnlyDictionary<string, System.Text.Json.JsonElement> Export()
    {
        var result = new Dictionary<string, System.Text.Json.JsonElement>();
        foreach (var (key, value) in _data)
        {
            if (value is System.Text.Json.JsonElement je)
            {
                result[key] = je;
            }
            else
            {
                var json = System.Text.Json.JsonSerializer.Serialize(value, value.GetType(), _jsonOptions);
                result[key] = System.Text.Json.JsonDocument.Parse(json).RootElement.Clone();
            }
        }
        return result;
    }

    /// <summary>
    /// 从 JsonElement 字典导入状态（覆盖现有数据）。
    /// 值保持 JsonElement 形式，在 Get 时延迟反序列化。
    /// </summary>
    public void Import(IReadOnlyDictionary<string, System.Text.Json.JsonElement> data)
    {
        _data.Clear();
        foreach (var (key, value) in data)
        {
            // 存储为 JsonElement，在 Get<T> 时延迟反序列化
            _data[key] = value.Clone();
        }
    }
}
