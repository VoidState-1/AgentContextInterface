using ACI.Core.Models;
using ACI.Core.Services;
using ACI.Framework.Components;
using ACI.Framework.Runtime;

namespace ACI.Framework.BuiltIn;

/// <summary>
/// Built-in activity log app. Subscribes to core events and emits compact log windows.
/// </summary>
public class ActivityLog : ContextApp
{
    private readonly List<LogItem> _logs = [];
    private int _windowCounter;
    private IDisposable? _actionSub;
    private IDisposable? _appSub;
    private IDisposable? _taskSub;

    public override string Name => "activity_log";

    public override string? AppDescription => "System activity log of actions, launches, and background tasks.";

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

    public override void OnDestroy()
    {
        _actionSub?.Dispose();
        _actionSub = null;

        _appSub?.Dispose();
        _appSub = null;

        _taskSub?.Dispose();
        _taskSub = null;
    }

    private void OnActionExecuted(ActionExecutedEvent evt)
    {
        var result = evt.Success ? "success" : "failed";
        var summary = evt.Summary != null ? $" ({evt.Summary})" : string.Empty;
        var text = $"[{evt.Seq}] action {evt.WindowId}.{evt.ActionId} -> {result}{summary}";
        AddLogWindow(evt.Seq, text);
    }

    private void OnAppCreated(AppCreatedEvent evt)
    {
        var target = evt.Target != null ? $", target {evt.Target}" : string.Empty;
        var text = $"[{evt.Seq}] launch app {evt.AppName}{target}";
        AddLogWindow(evt.Seq, text);
    }

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

    private class LogItem
    {
        public int Seq { get; init; }
        public required string WindowId { get; init; }
        public required string Text { get; init; }
        public bool IsPersistent { get; init; }
    }
}
