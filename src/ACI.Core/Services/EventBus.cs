using System.Collections.Concurrent;
using ACI.Core.Abstractions;

namespace ACI.Core.Services;

/// <summary>
/// 事件总线实现
/// </summary>
public class EventBus : IEventBus
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();
    private readonly object _lock = new();

    /// <summary>
    /// 发布事件
    /// </summary>
    public void Publish<T>(T evt) where T : IEvent
    {
        if (_handlers.TryGetValue(typeof(T), out var handlers))
        {
            List<Delegate> copy;
            lock (_lock)
            {
                copy = [.. handlers];
            }

            foreach (var handler in copy)
            {
                try
                {
                    ((Action<T>)handler)(evt);
                }
                catch
                {
                    // 忽略单个处理器的异常
                }
            }
        }
    }

    /// <summary>
    /// 订阅事件
    /// </summary>
    public IDisposable Subscribe<T>(Action<T> handler) where T : IEvent
    {
        var handlers = _handlers.GetOrAdd(typeof(T), _ => []);

        lock (_lock)
        {
            handlers.Add(handler);
        }

        return new Subscription(() =>
        {
            lock (_lock)
            {
                handlers.Remove(handler);
            }
        });
    }

    /// <summary>
    /// 订阅句柄
    /// </summary>
    private sealed class Subscription(Action onDispose) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                onDispose();
            }
        }
    }
}
