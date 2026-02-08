using ACI.Core.Abstractions;
using ACI.Core.Models;
using System.Threading;

namespace ACI.Framework.Runtime;

/// <summary>
/// 应用运行时上下文接口
/// </summary>
public interface IContext
{
    /// <summary>
    /// 窗口管理
    /// </summary>
    IWindowManager Windows { get; }

    /// <summary>
    /// 事件发布
    /// </summary>
    IEventBus Events { get; }

    /// <summary>
    /// 时钟（获取 seq）
    /// </summary>
    ISeqClock Clock { get; }

    /// <summary>
    /// 上下文管理（对话历史）
    /// </summary>
    IContextManager Context { get; }

    /// <summary>
    /// 请求刷新窗口（通知框架重新渲染窗口内容）
    /// </summary>
    void RequestRefresh(string windowId);

    /// <summary>
    /// 启动后台任务（立即返回，不阻塞当前会话交互）。
    /// </summary>
    /// <param name="windowId">关联窗口 ID</param>
    /// <param name="taskBody">后台任务主体</param>
    /// <param name="taskId">可选自定义任务 ID</param>
    /// <returns>实际任务 ID</returns>
    string StartBackgroundTask(
        string windowId,
        Func<CancellationToken, Task> taskBody,
        string? taskId = null);

    /// <summary>
    /// 取消后台任务。
    /// </summary>
    bool CancelBackgroundTask(string taskId);

    /// <summary>
    /// 将操作切回会话串行上下文执行（用于后台任务安全回写会话状态）。
    /// </summary>
    Task RunOnSessionAsync(Func<Task> action, CancellationToken ct = default);

    /// <summary>
    /// 获取服务
    /// </summary>
    T? GetService<T>() where T : class;
}
