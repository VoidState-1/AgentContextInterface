using ACI.Core.Models;
using ACI.Core.Services;
using ACI.Framework.Components;
using ACI.Framework.Runtime;

namespace ACI.Framework.BuiltIn;

/// <summary>
/// 内置活动日志应用，订阅核心事件并输出紧凑日志窗口。
/// </summary>
public class ActivityLog : ContextApp
{
    /// <summary>
    /// 日志存储状态。
    /// </summary>
    private readonly List<LogItem> _logs = [];
    private int _windowCounter;

    /// <summary>
    /// 事件订阅句柄。
    /// </summary>
    private IDisposable? _actionSub;
    private IDisposable? _appSub;
    private IDisposable? _taskSub;

    // 持久化键
    private const string LogsStateKey = "activity_logs";
    private const string CounterStateKey = "activity_counter";

    /// <summary>
    /// 应用名称。
    /// </summary>
    public override string Name => "activity_log";

    /// <summary>
    /// 应用描述。
    /// </summary>
    public override string? AppDescription => "System activity log of actions, launches, and background tasks.";

    /// <summary>
    /// 创建应用时注册事件订阅。
    /// </summary>
    public override void OnCreate()
    {
        RegisterToolNamespace(Name,
        [
            new ToolDescriptor
            {
                Id = "clear",
                Description = "Clear generated activity log windows."
            }
        ]);

        if (_actionSub != null || _appSub != null || _taskSub != null)
        {
            return;
        }

        _actionSub = Context.Events.Subscribe<ActionExecutedEvent>(OnActionExecuted);
        _appSub = Context.Events.Subscribe<AppCreatedEvent>(OnAppCreated);
        _taskSub = Context.Events.Subscribe<BackgroundTaskLifecycleEvent>(OnBackgroundTaskLifecycle);
    }

    /// <summary>
    /// 销毁应用时释放事件订阅。
    /// </summary>
    public override void OnDestroy()
    {
        _actionSub?.Dispose();
        _actionSub = null;

        _appSub?.Dispose();
        _appSub = null;

        _taskSub?.Dispose();
        _taskSub = null;
    }

    /// <summary>
    /// 将 _logs 和 _windowCounter 刷入 IAppState。
    /// </summary>
    public override void OnSaveState()
    {
        State.Set(LogsStateKey, _logs.Select(l => new LogItemDto
        {
            Seq = l.Seq,
            WindowId = l.WindowId,
            Text = l.Text,
            IsPersistent = l.IsPersistent
        }).ToList());
        State.Set(CounterStateKey, _windowCounter);
    }

    /// <summary>
    /// 从 IAppState 恢复 _logs 和 _windowCounter，然后重建日志窗口。
    /// </summary>
    public override void OnRestoreState()
    {
        _windowCounter = State.Get<int>(CounterStateKey);

        var savedLogs = State.Get<List<LogItemDto>>(LogsStateKey);
        _logs.Clear();

        if (savedLogs == null) return;

        foreach (var dto in savedLogs)
        {
            var logItem = new LogItem
            {
                Seq = dto.Seq,
                WindowId = dto.WindowId,
                Text = dto.Text,
                IsPersistent = dto.IsPersistent
            };
            _logs.Add(logItem);

            // 重建日志窗口
            var window = new Window
            {
                Id = dto.WindowId,
                Content = new Text(dto.Text),
                Options = new WindowOptions
                {
                    RenderMode = RenderMode.Compact,
                    Closable = false,
                    Important = dto.IsPersistent
                },
                AppName = Name
            };
            window.Meta.CreatedAt = dto.Seq;
            window.Meta.UpdatedAt = dto.Seq;

            Context.Windows.Add(window);
            RegisterWindow(dto.WindowId);
        }
    }

    /// <summary>
    /// 处理动作执行事件。
    /// </summary>
    private void OnActionExecuted(ActionExecutedEvent evt)
    {
        var result = evt.Success ? "success" : "failed";
        var summary = evt.Summary != null ? $" ({evt.Summary})" : string.Empty;
        var text = $"[{evt.Seq}] action {evt.WindowId}.{evt.ActionId} -> {result}{summary}";
        AddLogWindow(evt.Seq, text);
    }

    /// <summary>
    /// 处理应用创建事件。
    /// </summary>
    private void OnAppCreated(AppCreatedEvent evt)
    {
        var target = evt.Target != null ? $", target {evt.Target}" : string.Empty;
        var text = $"[{evt.Seq}] launch app {evt.AppName}{target}";
        AddLogWindow(evt.Seq, text);
    }

    /// <summary>
    /// 处理后台任务生命周期事件。
    /// </summary>
    private void OnBackgroundTaskLifecycle(BackgroundTaskLifecycleEvent evt)
    {
        var text = $"[{evt.Seq}] task {evt.TaskId} {evt.Status} window={evt.WindowId}";
        if (!string.IsNullOrWhiteSpace(evt.Source))
        {
            text += $" source={evt.Source}";
        }

        if (!string.IsNullOrWhiteSpace(evt.Message))
        {
            text += $" message={evt.Message}";
        }

        AddLogWindow(evt.Seq, text);
    }

    /// <summary>
    /// 追加一条日志并创建对应窗口。
    /// </summary>
    private void AddLogWindow(int seq, string text, bool isPersistent = false)
    {
        var windowId = $"log_{++_windowCounter}";

        var logItem = new LogItem
        {
            Seq = seq,
            WindowId = windowId,
            Text = text,
            IsPersistent = isPersistent
        };
        _logs.Add(logItem);

        var window = new Window
        {
            Id = windowId,
            Content = new Text(text),
            Options = new WindowOptions
            {
                RenderMode = RenderMode.Compact,
                Closable = false,
                Important = logItem.IsPersistent
            },
            AppName = Name
        };
        window.Meta.CreatedAt = seq;
        window.Meta.UpdatedAt = seq;

        Context.Windows.Add(window);
        RegisterWindow(windowId);
    }

    /// <summary>
    /// 创建日志主窗口。
    /// </summary>
    public override ContextWindow CreateWindow(string? intent)
    {
        return new ContextWindow
        {
            Id = "activity_log_main",
            Description = new Text("Activity log main window."),
            Content = new VStack
            {
                Children =
                [
                    new Text($"Current log entries: {_logs.Count}"),
                    new Text("Log entries are emitted as compact windows in context.")
                ]
            },
            NamespaceRefs = ["activity_log", "system"],
            Actions =
            [
                new ContextAction
                {
                    Id = "clear",
                    Label = "Clear Logs",
                    Handler = _ =>
                    {
                        foreach (var log in _logs.ToList())
                        {
                            Context.Windows.Remove(log.WindowId);
                            UnregisterWindow(log.WindowId);
                        }

                        _logs.Clear();
                        return Task.FromResult(ActionResult.Ok(
                            message: "Logs cleared",
                            shouldRefresh: true));
                    }
                }
            ]
        };
    }

    /// <summary>
    /// 单条日志记录模型（内部使用）。
    /// </summary>
    private class LogItem
    {
        public int Seq { get; init; }
        public required string WindowId { get; init; }
        public required string Text { get; init; }
        public bool IsPersistent { get; init; }
    }

    /// <summary>
    /// 日志记录 DTO（用于序列化/反序列化）。
    /// </summary>
    public class LogItemDto
    {
        public int Seq { get; set; }
        public string WindowId { get; set; } = "";
        public string Text { get; set; } = "";
        public bool IsPersistent { get; set; }
    }
}
