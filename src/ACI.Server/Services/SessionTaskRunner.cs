using System.Collections.Concurrent;
using ACI.Core.Abstractions;
using ACI.Core.Services;

namespace ACI.Server.Services;

/// <summary>
/// 会话级后台任务运行器。
/// </summary>
public sealed class SessionTaskRunner : IDisposable
{
    /// <summary>
    /// 任务状态存储。
    /// </summary>
    private readonly ConcurrentDictionary<string, RunningTask> _tasks = new(StringComparer.Ordinal);

    /// <summary>
    /// 事件发布依赖。
    /// </summary>
    private readonly IEventBus? _events;
    private readonly ISeqClock? _clock;

    /// <summary>
    /// 运行器状态。
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 创建会话任务运行器。
    /// </summary>
    public SessionTaskRunner(IEventBus? events = null, ISeqClock? clock = null)
    {
        _events = events;
        _clock = clock;
    }

    /// <summary>
    /// 启动后台任务并立即返回任务 ID。
    /// </summary>
    public string Start(
        string windowId,
        Func<CancellationToken, Task> taskBody,
        string? taskId = null,
        string? source = null)
    {
        // 1. 校验运行状态与入参。
        ObjectDisposedException.ThrowIf(_disposed, nameof(SessionTaskRunner));

        if (string.IsNullOrWhiteSpace(windowId))
        {
            throw new ArgumentException("windowId cannot be empty", nameof(windowId));
        }

        ArgumentNullException.ThrowIfNull(taskBody);

        // 2. 分配任务 ID 并登记到运行表。
        var resolvedTaskId = string.IsNullOrWhiteSpace(taskId)
            ? $"task_{Guid.NewGuid():N}"
            : taskId;

        var record = new RunningTask(windowId);
        if (!_tasks.TryAdd(resolvedTaskId, record))
        {
            throw new InvalidOperationException($"background task id conflict: {resolvedTaskId}");
        }

        PublishLifecycle(resolvedTaskId, windowId, BackgroundTaskStatus.Started, source);

        // 3. 在线程池中执行任务，并记录取消/失败状态。
        record.Work = Task.Run(async () =>
        {
            var canceled = false;
            string? errorMessage = null;

            try
            {
                await taskBody(record.Cancellation.Token);
            }
            catch (OperationCanceledException) when (record.Cancellation.IsCancellationRequested)
            {
                canceled = true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
            }
            finally
            {
                _tasks.TryRemove(resolvedTaskId, out _);
                record.Cancellation.Dispose();

                if (errorMessage != null)
                {
                    PublishLifecycle(resolvedTaskId, windowId, BackgroundTaskStatus.Failed, source, errorMessage);
                }
                else if (canceled)
                {
                    PublishLifecycle(resolvedTaskId, windowId, BackgroundTaskStatus.Canceled, source);
                }
                else
                {
                    PublishLifecycle(resolvedTaskId, windowId, BackgroundTaskStatus.Completed, source);
                }
            }
        });

        // 4. 同步返回任务 ID，避免阻塞主交互流程。
        return resolvedTaskId;
    }

    /// <summary>
    /// 请求取消指定后台任务。
    /// </summary>
    public bool Cancel(string taskId)
    {
        if (_disposed || string.IsNullOrWhiteSpace(taskId))
        {
            return false;
        }

        if (!_tasks.TryGetValue(taskId, out var task))
        {
            return false;
        }

        task.Cancellation.Cancel();
        return true;
    }

    /// <summary>
    /// 释放运行器并取消所有未完成任务。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var task in _tasks.Values)
        {
            task.Cancellation.Cancel();
        }
    }

    /// <summary>
    /// 发布后台任务生命周期事件。
    /// </summary>
    private void PublishLifecycle(
        string taskId,
        string windowId,
        BackgroundTaskStatus status,
        string? source,
        string? message = null)
    {
        if (_events == null)
        {
            return;
        }

        var seq = _clock?.Next() ?? 0;
        _events.Publish(new BackgroundTaskLifecycleEvent(seq, taskId, windowId, status, source, message));
    }

    /// <summary>
    /// 运行中的后台任务记录。
    /// </summary>
    private sealed class RunningTask
    {
        /// <summary>
        /// 创建运行任务记录。
        /// </summary>
        public RunningTask(string windowId)
        {
            WindowId = windowId;
        }

        /// <summary>
        /// 关联窗口 ID。
        /// </summary>
        public string WindowId { get; }

        /// <summary>
        /// 任务取消令牌源。
        /// </summary>
        public CancellationTokenSource Cancellation { get; } = new();

        /// <summary>
        /// 正在执行的任务句柄。
        /// </summary>
        public Task Work { get; set; } = Task.CompletedTask;
    }
}
