using ContextUI.Core.Abstractions;
using ContextUI.Core.Models;
using ContextUI.Core.Services;
using ContextUI.Framework.Runtime;
using ContextUI.LLM;
using ContextUI.LLM.Abstractions;
using ContextUI.Server.Settings;
using System.Threading;

namespace ContextUI.Server.Services;

/// <summary>
/// 会话上下文 - 每个用户会话的完整运行环境
/// </summary>
public class SessionContext : IDisposable
{
    public string SessionId { get; }
    public DateTime CreatedAt { get; }

    // Core 服务
    public ISeqClock Clock { get; }
    public IEventBus Events { get; }
    public IWindowManager Windows { get; }
    public IContextManager Context { get; }

    // Framework 服务
    public RuntimeContext Runtime { get; }
    public FrameworkHost Host { get; }

    // LLM 服务
    public InteractionController Interaction { get; }
    public ActionExecutor ActionExecutor { get; }

    private bool _disposed;
    private readonly SemaphoreSlim _sessionLock = new(1, 1);

    public SessionContext(
        string sessionId,
        ILLMBridge llmBridge,
        ContextUIOptions options,
        Action<FrameworkHost>? configureApps = null)
    {
        SessionId = sessionId;
        CreatedAt = DateTime.UtcNow;

        var maxTokens = Math.Max(1000, options.Render.MaxTokens);
        var minConversationTokens = Math.Clamp(options.Render.MinConversationTokens, 0, maxTokens);
        var maxContextItems = Math.Max(10, options.Context.MaxItems);
        var maxLogs = Math.Max(10, options.ActivityLog.MaxLogs);

        // 创建 Core 服务
        Clock = new SeqClock();
        Events = new EventBus();
        Windows = new WindowManager(Clock);
        Context = new ContextManager(Clock);
        Windows.OnChanged += OnWindowChanged;

        // 创建 Framework 服务
        Runtime = new RuntimeContext(Windows, Events, Clock, Context);
        Host = new FrameworkHost(Runtime);
        ActionExecutor = new ActionExecutor(Windows, Clock, Events, Host.RefreshWindow);

        // 注册内置应用
        RegisterBuiltInApps(maxLogs);

        // 允许外部配置额外的应用
        configureApps?.Invoke(Host);

        // 创建 LLM 服务
        Interaction = new InteractionController(
            llmBridge,
            Host,
            Context,
            Windows,
            ActionExecutor,
            renderOptions: new RenderOptions
            {
                MaxTokens = maxTokens,
                MinConversationTokens = minConversationTokens
            },
            maxContextItems: maxContextItems
        );
    }

    /// <summary>
    /// 注册内置应用
    /// </summary>
    private void RegisterBuiltInApps(int maxLogs)
    {
        // 注册应用启动器
        Host.Register(new Framework.BuiltIn.AppLauncher(() => Host.GetApps().ToList()));

        // 注册活动日志
        Host.Register(new Framework.BuiltIn.ActivityLog(maxLogs));

        // 注册文件浏览器
        Host.Register(new Framework.BuiltIn.FileExplorerApp());
    }

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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Windows.OnChanged -= OnWindowChanged;
        _sessionLock.Dispose();

        // 清理资源
        GC.SuppressFinalize(this);
    }
}
