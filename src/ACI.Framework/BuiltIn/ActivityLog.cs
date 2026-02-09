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
        // 1. 构建主窗口内容。
        // 2. 暴露清理日志与关闭窗口两个动作。
        // 3. 返回主窗口定义。
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
                },
                new ContextAction
                {
                    Id = "close",
                    Label = "Close",
                    Handler = _ => Task.FromResult(ActionResult.Close())
                }
            ]
        };
    }

    /// <summary>
    /// 单条日志记录模型。
    /// </summary>
    private class LogItem
    {
        /// <summary>
        /// 事件序号。
        /// </summary>
        public int Seq { get; init; }

        /// <summary>
        /// 日志窗口 ID。
        /// </summary>
        public required string WindowId { get; init; }

        /// <summary>
        /// 日志文本内容。
        /// </summary>
        public required string Text { get; init; }

        /// <summary>
        /// 是否持久化为重要日志。
        /// </summary>
        public bool IsPersistent { get; init; }
    }
}
