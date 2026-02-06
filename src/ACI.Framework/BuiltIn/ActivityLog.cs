using ACI.Core.Abstractions;
using ACI.Core.Models;
using ACI.Core.Services;
using ACI.Framework.Components;
using ACI.Framework.Runtime;

namespace ACI.Framework.BuiltIn;

/// <summary>
/// 活动日志 - 内置应用
/// 订阅系统事件，为每个事件创建精简窗口
/// </summary>
public class ActivityLog : ContextApp
{
    private readonly List<LogItem> _logs = [];
    private readonly int _maxLogs;
    private int _windowCounter = 0;
    private IDisposable? _actionSub;
    private IDisposable? _appSub;

    public ActivityLog(int maxLogs = 50)
    {
        _maxLogs = Math.Max(10, maxLogs);
    }

    public override string Name => "activity_log";

    public override string? AppDescription => "系统活动日志，记录所有操作";

    public override void OnCreate()
    {
        if (_actionSub != null || _appSub != null)
        {
            return;
        }

        // 订阅操作执行事件
        _actionSub = Context.Events.Subscribe<ActionExecutedEvent>(OnActionExecuted);

        // 订阅应用创建事件
        _appSub = Context.Events.Subscribe<AppCreatedEvent>(OnAppCreated);
    }

    public override void OnDestroy()
    {
        _actionSub?.Dispose();
        _actionSub = null;

        _appSub?.Dispose();
        _appSub = null;
    }

    private void OnActionExecuted(ActionExecutedEvent evt)
    {
        var result = evt.Success ? "成功" : "失败";
        var summary = evt.Summary != null ? $" ({evt.Summary})" : "";
        var text = $"[{evt.Seq}] 操作 {evt.WindowId}.{evt.ActionId} -> {result}{summary}";

        AddLogWindow(evt.Seq, text);
    }

    private void OnAppCreated(AppCreatedEvent evt)
    {
        var target = evt.Target != null ? $"，目标: {evt.Target}" : "";
        var text = $"[{evt.Seq}] 打开应用 {evt.AppName}{target}";

        AddLogWindow(evt.Seq, text);
    }

    private void AddLogWindow(int seq, string text)
    {
        var windowId = $"log_{++_windowCounter}";

        _logs.Add(new LogItem
        {
            Seq = seq,
            WindowId = windowId,
            Text = text
        });

        // 创建精简窗口
        var window = new Window
        {
            Id = windowId,
            Content = new Text(text),
            Options = new WindowOptions
            {
                RenderMode = RenderMode.Compact,
                Closable = false
            },
            AppName = Name
        };
        window.Meta.CreatedAt = seq;
        window.Meta.UpdatedAt = seq;

        Context.Windows.Add(window);
        RegisterWindow(windowId);

        // 检查是否需要压缩
        CompactIfNeeded();
    }

    /// <summary>
    /// 压缩日志（移除旧的非持久日志）
    /// </summary>
    private void CompactIfNeeded()
    {
        while (_logs.Count > _maxLogs)
        {
            var oldest = _logs.FirstOrDefault(l => !l.IsPersistent);
            if (oldest == null) break;

            _logs.Remove(oldest);
            Context.Windows.Remove(oldest.WindowId);
            UnregisterWindow(oldest.WindowId);
        }
    }

    public override ContextWindow CreateWindow(string? intent)
    {
        // 日志应用的主窗口可以显示日志统计信息
        return new ContextWindow
        {
            Id = "activity_log_main",
            Description = new Text("活动日志主窗口。日志条目以精简窗口形式穿插在上下文中。"),
            Content = new VStack
            {
                Children =
                [
                    new Text($"当前日志条目数: {_logs.Count}"),
                    new Text("日志条目以精简窗口形式显示在上下文中。")
                ]
            },
            Actions =
            [
                new ContextAction
                {
                    Id = "clear",
                    Label = "清除所有日志",
                    Handler = _ =>
                    {
                        foreach (var log in _logs.ToList())
                        {
                            Context.Windows.Remove(log.WindowId);
                            UnregisterWindow(log.WindowId);
                        }
                        _logs.Clear();
                        return Task.FromResult(ActionResult.Ok(
                            message: "日志已清除",
                            shouldRefresh: true
                        ));
                    }
                },
                new ContextAction
                {
                    Id = "close",
                    Label = "关闭",
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
