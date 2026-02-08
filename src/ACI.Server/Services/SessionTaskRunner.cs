using System.Collections.Concurrent;

namespace ACI.Server.Services;

/// <summary>
/// 会话级后台任务运行器。
/// 负责后台任务的启动、取消和生命周期收敛。
/// </summary>
public sealed class SessionTaskRunner : IDisposable
{
    private readonly ConcurrentDictionary<string, RunningTask> _tasks = new(StringComparer.Ordinal);
    private bool _disposed;

    /// <summary>
    /// 启动后台任务（立即返回 taskId）。
    /// </summary>
    public string Start(
        string windowId,
        Func<CancellationToken, Task> taskBody,
        string? taskId = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(SessionTaskRunner));

        if (string.IsNullOrWhiteSpace(windowId))
        {
            throw new ArgumentException("windowId 不能为空", nameof(windowId));
        }

        if (taskBody == null)
        {
            throw new ArgumentNullException(nameof(taskBody));
        }

        // 允许调用方传入稳定 taskId；未提供时自动生成。
        var resolvedTaskId = string.IsNullOrWhiteSpace(taskId)
            ? $"task_{Guid.NewGuid():N}"
            : taskId;

        var record = new RunningTask(windowId);
        // 任务先注册后启动，确保外部在任务真正运行前也能通过 taskId 取消。
        if (!_tasks.TryAdd(resolvedTaskId, record))
        {
            throw new InvalidOperationException($"后台任务 ID 冲突: {resolvedTaskId}");
        }

        // Fire-and-forget：任务在后台执行，不阻塞当前会话请求链路。
        record.Work = Task.Run(async () =>
        {
            try
            {
                await taskBody(record.Cancellation.Token);
            }
            catch (OperationCanceledException) when (record.Cancellation.IsCancellationRequested)
            {
                // 任务被取消，正常结束。
            }
            finally
            {
                // 无论成功/失败/取消，统一从索引移除并释放 token 资源。
                _tasks.TryRemove(resolvedTaskId, out _);
                record.Cancellation.Dispose();
            }
        });

        return resolvedTaskId;
    }

    /// <summary>
    /// 取消后台任务。
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

        // 仅发送取消信号；实际结束由任务自身在下一个可取消点完成。
        task.Cancellation.Cancel();
        return true;
    }

    /// <summary>
    /// 取消并释放全部后台任务。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        // 会话销毁时对全部后台任务广播取消，避免悬挂任务继续写会话状态。
        foreach (var task in _tasks.Values)
        {
            task.Cancellation.Cancel();
        }
    }

    /// <summary>
    /// 内部状态
    /// </summary>
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
