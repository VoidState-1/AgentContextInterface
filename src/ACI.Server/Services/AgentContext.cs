using ACI.Core.Abstractions;
using ACI.Core.Models;
using ACI.Core.Services;
using ACI.Framework.BuiltIn;
using ACI.Framework.Runtime;
using ACI.LLM;
using ACI.LLM.Abstractions;
using ACI.Storage;
using ACI.Server.Settings;
using System.Threading;

namespace ACI.Server.Services;

/// <summary>
/// 单个 Agent 的运行时容器，聚合 Core / Framework / LLM 三层服务。
/// 原 SessionContext 重命名而来，现在代表一个 Agent 而非一个 Session。
/// </summary>
public class AgentContext : IDisposable
{
    /// <summary>
    /// Agent 身份信息
    /// </summary>
    public AgentProfile Profile { get; }

    /// <summary>
    /// Agent ID（快捷访问）
    /// </summary>
    public string AgentId => Profile.Id;

    public DateTime CreatedAt { get; }

    /// <summary>
    /// Core 层服务
    /// </summary>
    public ISeqClock Clock { get; }
    public IEventBus Events { get; }
    public IWindowManager Windows { get; }
    public IContextManager Context { get; }
    public IToolNamespaceRegistry ToolNamespaces { get; }

    /// <summary>
    /// Framework 层服务
    /// </summary>
    public RuntimeContext Runtime { get; }
    public FrameworkHost Host { get; }

    /// <summary>
    /// 消息频道（供 Session 层桥接使用）
    /// </summary>
    public LocalMessageChannel LocalMessageChannel { get; }

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
    /// 构建并初始化 Agent 上下文。
    /// </summary>
    /// <param name="profile">Agent 身份配置</param>
    /// <param name="llmBridge">LLM 桥接</param>
    /// <param name="options">ACI 配置选项</param>
    /// <param name="registerMailbox">是否注册 MailboxApp（多 Agent 时为 true）</param>
    /// <param name="configureApps">外部应用注册回调</param>
    public AgentContext(
        AgentProfile profile,
        ILLMBridge llmBridge,
        ACIOptions options,
        bool registerMailbox = false,
        Action<FrameworkHost>? configureApps = null)
    {
        // 1. 初始化身份
        Profile = profile;
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
        ToolNamespaces = new ToolNamespaceRegistry();
        Windows.OnChanged += OnWindowChanged;

        // 4. 初始化 Framework 层（含 MessageChannel）
        LocalMessageChannel = new LocalMessageChannel(profile.Id);
        Runtime = new RuntimeContext(Windows, Events, Clock, Context, ToolNamespaces, profile, LocalMessageChannel);
        _taskRunner = new SessionTaskRunner(Events, Clock);
        Runtime.ConfigureBackgroundTaskHandlers(
            (windowId, taskBody, taskId) => _taskRunner.Start(windowId, taskBody, taskId),
            taskId => _taskRunner.Cancel(taskId),
            RunSerializedActionAsync);

        // 5. 初始化服务和执行器
        Host = new FrameworkHost(Runtime);
        ActionExecutor = new ActionExecutor(Windows, Clock, Events, Host.RefreshWindow);

        // 6. 注册内置应用
        RegisterSystemNamespace();
        RegisterBuiltInApps(registerMailbox);
        Host.Start("activity_log");

        configureApps?.Invoke(Host);
        Host.Launch("launcher");

        // 7. 初始化 InteractionController（使用 Profile 构建提示词）
        Interaction = new InteractionController(
            llmBridge,
            Host,
            Context,
            Windows,
            ToolNamespaces,
            ActionExecutor,
            renderOptions: new RenderOptions
            {
                MaxTokens = maxTokens,
                MinConversationTokens = minConversationTokens,
                PruneTargetTokens = pruneTargetTokens
            },
            startBackgroundTask: StartInteractionBackgroundTask,
            agentProfile: profile
        );
    }

    /// <summary>
    /// 注册内置应用。
    /// </summary>
    private void RegisterBuiltInApps(bool registerMailbox)
    {
        Host.Register(new AppLauncher(() => Host.GetApps().ToList()));
        Host.Register(new ActivityLog());
        Host.Register(new FileExplorerApp());

        if (registerMailbox)
        {
            Host.Register(new MailboxApp());
        }
    }

    /// <summary>
    /// 注册系统命名空间工具。
    /// </summary>
    private void RegisterSystemNamespace()
    {
        ToolNamespaces.Upsert(new ToolNamespaceDefinition
        {
            Id = "system",
            Tools =
            [
                new ToolDescriptor
                {
                    Id = "close",
                    Params = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["summary"] = "string?"
                    },
                    Description = "Close the target window."
                }
            ]
        });
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
        if (_disposed) return;

        try
        {
            await RunSerializedAsync(async () =>
            {
                if (_disposed) return true;
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

    // ========== 快照支持 ==========

    /// <summary>
    /// 采集当前 Agent 快照。
    /// 包含：Profile、Clock 序号、上下文时间线、应用快照。
    /// </summary>
    public AgentSnapshot TakeSnapshot()
    {
        var snapshot = new AgentSnapshot
        {
            Profile = AgentProfileSnapshot.From(Profile),
            ClockSeq = Clock is SeqClock sc ? sc.CurrentSeq : 0,
            ContextItems = [],
            Apps = Host.TakeAppSnapshots()
        };

        // 导出上下文时间线
        if (Context is ContextManager cm)
        {
            var store = cm.Store;
            if (store is ContextStore cs)
            {
                foreach (var item in cs.ExportItems())
                {
                    snapshot.ContextItems.Add(ContextItemSnapshot.From(item));
                }
            }
        }

        return snapshot;
    }

    /// <summary>
    /// 从快照恢复 Agent 状态。
    /// 恢复顺序：Clock → ContextStore → FrameworkHost Apps。
    /// 注意：需要在 Agent 初始化之后、首次交互之前调用。
    /// </summary>
    public void RestoreFromSnapshot(AgentSnapshot snapshot)
    {
        // 1. 恢复时钟
        if (Clock is SeqClock sc)
        {
            sc.Reset(snapshot.ClockSeq);
        }

        // 2. 恢复上下文时间线
        if (Context is ContextManager cm && cm.Store is ContextStore cs)
        {
            var items = snapshot.ContextItems
                .Select(s => s.ToContextItem())
                .ToList();
            cs.ImportItems(items);
        }

        // 3. 恢复应用状态
        Host.RestoreAppSnapshots(snapshot.Apps);
    }

    /// <summary>
    /// 释放资源。
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
