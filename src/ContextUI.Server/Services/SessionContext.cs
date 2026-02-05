using ContextUI.Core.Abstractions;
using ContextUI.Core.Services;
using ContextUI.Framework.Runtime;
using ContextUI.LLM;
using ContextUI.LLM.Abstractions;

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

    private bool _disposed;

    public SessionContext(
        string sessionId,
        ILLMBridge llmBridge,
        Action<FrameworkHost>? configureApps = null)
    {
        SessionId = sessionId;
        CreatedAt = DateTime.UtcNow;

        // 创建 Core 服务
        Clock = new SeqClock();
        Events = new EventBus();
        Windows = new WindowManager(Clock);
        Context = new ContextManager(Clock);

        // 创建 Framework 服务
        Runtime = new RuntimeContext(Windows, Events, Clock, Context);
        Host = new FrameworkHost(Runtime);

        // 注册内置应用
        RegisterBuiltInApps();

        // 允许外部配置额外的应用
        configureApps?.Invoke(Host);

        // 创建 LLM 服务
        Interaction = new InteractionController(
            llmBridge,
            Host,
            Context,
            Windows
        );
    }

    /// <summary>
    /// 注册内置应用
    /// </summary>
    private void RegisterBuiltInApps()
    {
        // 注册应用启动器
        Host.Register(new Framework.BuiltIn.AppLauncher(() => Host.GetApps().ToList()));

        // 注册活动日志
        Host.Register(new Framework.BuiltIn.ActivityLog());
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // 清理资源
        GC.SuppressFinalize(this);
    }
}
