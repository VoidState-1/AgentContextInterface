using ACI.Core.Abstractions;

namespace ACI.Core.Services;

public enum BackgroundTaskStatus
{
    Started,
    Completed,
    Failed,
    Canceled
}

/// <summary>
/// Unified lifecycle event for background task execution.
/// </summary>
public record BackgroundTaskLifecycleEvent(
    int Seq,
    string TaskId,
    string WindowId,
    BackgroundTaskStatus Status,
    string? Source = null,
    string? Message = null
) : IEvent;
