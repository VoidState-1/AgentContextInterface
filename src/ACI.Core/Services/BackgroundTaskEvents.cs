using ACI.Core.Abstractions;

namespace ACI.Core.Services;

/// <summary>
/// 后台任务状态枚举。
/// </summary>
public enum BackgroundTaskStatus
{
    /// <summary>
    /// 任务已启动。
    /// </summary>
    Started,
    /// <summary>
    /// 任务已完成。
    /// </summary>
    Completed,
    /// <summary>
    /// 任务执行失败。
    /// </summary>
    Failed,
    /// <summary>
    /// 任务已取消。
    /// </summary>
    Canceled
}

/// <summary>
/// 后台任务生命周期事件。
/// </summary>
public record BackgroundTaskLifecycleEvent : IEvent
{
    /// <summary>
    /// 事件序号。
    /// </summary>
    public int Seq { get; init; }

    /// <summary>
    /// 任务 ID。
    /// </summary>
    public string TaskId { get; init; }

    /// <summary>
    /// 关联窗口 ID。
    /// </summary>
    public string WindowId { get; init; }

    /// <summary>
    /// 当前生命周期状态。
    /// </summary>
    public BackgroundTaskStatus Status { get; init; }

    /// <summary>
    /// 任务来源标识。
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// 附加消息（通常用于失败原因）。
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// 创建后台任务生命周期事件。
    /// </summary>
    public BackgroundTaskLifecycleEvent(
        int seq,
        string taskId,
        string windowId,
        BackgroundTaskStatus status,
        string? source = null,
        string? message = null)
    {
        Seq = seq;
        TaskId = taskId;
        WindowId = windowId;
        Status = status;
        Source = source;
        Message = message;
    }
}
