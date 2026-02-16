using System.Threading;
using ACI.Core.Abstractions;
using ACI.Core.Models;

namespace ACI.Framework.Runtime;

/// <summary>
/// 应用运行时上下文接口。
/// </summary>
public interface IContext
{
    /// <summary>
    /// 窗口管理器。
    /// </summary>
    IWindowManager Windows { get; }

    /// <summary>
    /// 事件总线。
    /// </summary>
    IEventBus Events { get; }

    /// <summary>
    /// 序号时钟。
    /// </summary>
    ISeqClock Clock { get; }

    /// <summary>
    /// 上下文管理器。
    /// </summary>
    IContextManager Context { get; }

    /// <summary>
    /// 工具命名空间注册表。
    /// </summary>
    IToolNamespaceRegistry ToolNamespaces { get; }

    /// <summary>
    /// 请求刷新窗口。
    /// </summary>
    void RequestRefresh(string windowId);

    /// <summary>
    /// 启动后台任务。
    /// </summary>
    string StartBackgroundTask(
        string windowId,
        Func<CancellationToken, Task> taskBody,
        string? taskId = null);

    /// <summary>
    /// 取消后台任务。
    /// </summary>
    bool CancelBackgroundTask(string taskId);

    /// <summary>
    /// 在会话串行上下文中执行动作。
    /// </summary>
    Task RunOnSessionAsync(Func<Task> action, CancellationToken ct = default);

    /// <summary>
    /// 获取服务。
    /// </summary>
    T? GetService<T>() where T : class;

    /// <summary>
    /// 当前 Agent 配置。
    /// </summary>
    AgentProfile Profile { get; }

    /// <summary>
    /// 应用间消息通道。
    /// </summary>
    IMessageChannel MessageChannel { get; }
}
