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
    /// <summary>
    /// 对话ID信息
    /// </summary>
    public string SessionId { get; }
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Core 层服务
    /// </summary>
    public ISeqClock Clock { get; }
    public IEventBus Events { get; }
    public IWindowManager Windows { get; }
    public IContextManager Context { get; }

    /// <summary>
    /// Framework 层服务
    /// </summary>
    public RuntimeContext Runtime { get; }
    public FrameworkHost Host { get; }

    /// <summary>
    /// LLM 层服务和执行器
    /// </summary>
    public InteractionController Interaction { get; }
    public ActionExecutor ActionExecutor { get; }

    /// <summary>
    /// 内部状态
    /// </summary>
    private readonly SessionTaskRunner _taskRunner;
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
        // 1. 初始化 Session
        SessionId = sessionId;
        CreatedAt = DateTime.UtcNow;

        // 2. 初始化 RenderOptions
        var maxTokens = Math.Max(1000, options.Render.MaxTokens);
        var pruneTargetTokens = options.Render.PruneTargetTokens <= 0
            ? Math.Max(1, maxTokens / 2)
            : Math.Clamp(options.Render.PruneTargetTokens, 1, maxTokens);
        var minConversationTokens = Math.Clamp(options.Render.MinConversationTokens, 0, pruneTargetTokens);

        // 3. 初始化 Core 层
        Clock = new SeqClock();
        Events = new EventBus();
        Windows = new WindowManager(Clock);
        Context = new ContextManager(Clock);
        Windows.OnChanged += OnWindowChanged;

        // 4. 初始化 framework 层
        Runtime = new RuntimeContext(Windows, Events, Clock, Context);
        _taskRunner = new SessionTaskRunner(Events, Clock);
        Runtime.ConfigureBackgroundTaskHandlers(
            (windowId, taskBody, taskId) => _taskRunner.Start(windowId, taskBody, taskId),
            taskId => _taskRunner.Cancel(taskId),
            RunSerializedActionAsync);

        // 5. 初始化服务和执行器
        Host = new FrameworkHost(Runtime);
        ActionExecutor = new ActionExecutor(Windows, Clock, Events, Host.RefreshWindow);

        // 6. 注册内置应用
        RegisterBuiltInApps();
        Host.Start("activity_log");

        configureApps?.Invoke(Host);
        // 启动器窗口改为会话初始化即常驻。
        Host.Launch("launcher");

        // 7. 初始化 InteractionController
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
                PruneTargetTokens = pruneTargetTokens
            },
            startBackgroundTask: StartInteractionBackgroundTask
        );
    }

    /// <summary>
    /// 注册内置应用。
    /// </summary>
    private void RegisterBuiltInApps()
    {
        Host.Register(new Framework.BuiltIn.AppLauncher(() => Host.GetApps().ToList()));
        Host.Register(new Framework.BuiltIn.ActivityLog());
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
    /// 后台任务回写会话状态时，统一回到串行执行上下文。
    /// </summary>
    private async Task RunSerializedActionAsync(Func<Task> action, CancellationToken ct = default)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            await RunSerializedAsync(async () =>
            {
                if (_disposed)
                {
                    return true;
                }

                await action();
                return true;
            }, ct);
        }
        catch (ObjectDisposedException)
        {
            // 会话已释放，忽略后台任务回写。
        }
    }

    /// <summary>
    /// 启动由交互触发的异步动作，并统一回到会话串行上下文写入状态。
    /// </summary>
    private string StartInteractionBackgroundTask(
        string windowId,
        Func<CancellationToken, Task> taskBody,
        string? taskId)
    {
        return _taskRunner.Start(
            windowId,
            token => RunSerializedActionAsync(() => taskBody(token), token),
            taskId,
            source: "interaction_action");
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
        _taskRunner.Dispose();
        Windows.OnChanged -= OnWindowChanged;
        _sessionLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
