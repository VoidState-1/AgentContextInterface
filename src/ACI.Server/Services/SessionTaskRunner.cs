using System.Collections.Concurrent;
using ACI.Core.Abstractions;
using ACI.Core.Services;

namespace ACI.Server.Services;

/// <summary>
/// Session-scoped background task runner.
/// </summary>
public sealed class SessionTaskRunner : IDisposable
{
    private readonly ConcurrentDictionary<string, RunningTask> _tasks = new(StringComparer.Ordinal);
    private readonly IEventBus? _events;
    private readonly ISeqClock? _clock;
    private bool _disposed;

    public SessionTaskRunner(IEventBus? events = null, ISeqClock? clock = null)
    {
        _events = events;
        _clock = clock;
    }

    /// <summary>
    /// Starts a background task and returns immediately with task id.
    /// </summary>
    public string Start(
        string windowId,
        Func<CancellationToken, Task> taskBody,
        string? taskId = null,
        string? source = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(SessionTaskRunner));

        if (string.IsNullOrWhiteSpace(windowId))
        {
            throw new ArgumentException("windowId cannot be empty", nameof(windowId));
        }

        ArgumentNullException.ThrowIfNull(taskBody);

        var resolvedTaskId = string.IsNullOrWhiteSpace(taskId)
            ? $"task_{Guid.NewGuid():N}"
            : taskId;

        var record = new RunningTask(windowId);
        if (!_tasks.TryAdd(resolvedTaskId, record))
        {
            throw new InvalidOperationException($"background task id conflict: {resolvedTaskId}");
        }

        PublishLifecycle(resolvedTaskId, windowId, BackgroundTaskStatus.Started, source);

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

        return resolvedTaskId;
    }

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

    private sealed class RunningTask
    {
        public RunningTask(string windowId)
        {
            WindowId = windowId;
        }

        public string WindowId { get; }
        public CancellationTokenSource Cancellation { get; } = new();
        public Task Work { get; set; } = Task.CompletedTask;
    }
}
