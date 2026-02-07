using ACI.Core.Abstractions;
using ACI.Core.Models;
using ACI.Core.Services;
using ACI.Framework.Runtime;
using ACI.LLM;
using ACI.LLM.Abstractions;
using ACI.Server.Settings;
using System.Threading;

namespace ACI.Server.Services;

/// <summary>
/// 单个会话的运行时容器，聚合 Core / Framework / LLM 三层服务。
/// </summary>
public class SessionContext : IDisposable
{
    public string SessionId { get; }
    public DateTime CreatedAt { get; }

    public ISeqClock Clock { get; }
    public IEventBus Events { get; }
    public IWindowManager Windows { get; }
    public IContextManager Context { get; }

    public RuntimeContext Runtime { get; }
    public FrameworkHost Host { get; }

    public InteractionController Interaction { get; }
    public ActionExecutor ActionExecutor { get; }

    private bool _disposed;
    private readonly SemaphoreSlim _sessionLock = new(1, 1);

    /// <summary>
    /// 构建并初始化会话上下文。
    /// </summary>
    public SessionContext(
        string sessionId,
        ILLMBridge llmBridge,
        ACIOptions options,
        Action<FrameworkHost>? configureApps = null)
    {
        SessionId = sessionId;
        CreatedAt = DateTime.UtcNow;

        var maxTokens = Math.Max(1000, options.Render.MaxTokens);
        var trimToTokens = options.Render.TrimToTokens <= 0
            ? Math.Max(1, maxTokens / 2)
            : Math.Clamp(options.Render.TrimToTokens, 1, maxTokens);
        var minConversationTokens = Math.Clamp(options.Render.MinConversationTokens, 0, trimToTokens);
        var maxLogs = Math.Max(10, options.ActivityLog.MaxLogs);

        Clock = new SeqClock();
        Events = new EventBus();
        Windows = new WindowManager(Clock);
        Context = new ContextManager(Clock);
        Windows.OnChanged += OnWindowChanged;

        Runtime = new RuntimeContext(Windows, Events, Clock, Context);
        Host = new FrameworkHost(Runtime);
        ActionExecutor = new ActionExecutor(Windows, Clock, Events, Host.RefreshWindow);

        RegisterBuiltInApps(maxLogs);
        Host.Start("activity_log");

        configureApps?.Invoke(Host);
        // 启动器窗口改为会话初始化即常驻。
        Host.Launch("launcher");

        Interaction = new InteractionController(
            llmBridge,
            Host,
            Context,
            Windows,
            ActionExecutor,
            renderOptions: new RenderOptions
            {
                MaxTokens = maxTokens,
                MinConversationTokens = minConversationTokens,
                TrimToTokens = trimToTokens
            }
        );
    }

    /// <summary>
    /// 注册内置应用。
    /// </summary>
    private void RegisterBuiltInApps(int maxLogs)
    {
        Host.Register(new Framework.BuiltIn.AppLauncher(() => Host.GetApps().ToList()));
        Host.Register(new Framework.BuiltIn.ActivityLog(maxLogs));
        Host.Register(new Framework.BuiltIn.FileExplorerApp());
    }

    /// <summary>
    /// 串行执行会话内任务，避免并发修改状态。
    /// </summary>
    public async Task<T> RunSerializedAsync<T>(Func<Task<T>> action, CancellationToken ct = default)
    {
        await _sessionLock.WaitAsync(ct);
        try
        {
            return await action();
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    /// <summary>
    /// 将窗口生命周期变化同步到上下文时间线。
    /// </summary>
    private void OnWindowChanged(WindowChangedEvent evt)
    {
        if (evt.Type == WindowEventType.Created)
        {
            Context.Add(new ContextItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = ContextItemType.Window,
                Content = evt.WindowId
            });
            return;
        }

        if (evt.Type == WindowEventType.Removed)
        {
            Context.MarkWindowObsolete(evt.WindowId);
            return;
        }

        if (evt.Type == WindowEventType.Updated &&
            evt.Window?.Options.RefreshMode == RefreshMode.Append)
        {
            Context.Add(new ContextItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = ContextItemType.Window,
                Content = evt.WindowId
            });
        }
    }

    /// <summary>
    /// 释放会话资源。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Windows.OnChanged -= OnWindowChanged;
        _sessionLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
