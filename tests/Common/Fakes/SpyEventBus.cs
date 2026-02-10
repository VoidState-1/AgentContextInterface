using ACI.Core.Abstractions;

namespace ACI.Tests.Common.Fakes;

public sealed class SpyEventBus : IEventBus
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = [];

    public List<object> PublishedEvents { get; } = [];

    public void Publish<T>(T evt) where T : IEvent
    {
        PublishedEvents.Add(evt);

        if (!_handlers.TryGetValue(typeof(T), out var handlers))
        {
            return;
        }

        foreach (var handler in handlers.Cast<Action<T>>())
        {
            handler(evt);
        }
    }

    public IDisposable Subscribe<T>(Action<T> handler) where T : IEvent
    {
        if (!_handlers.TryGetValue(typeof(T), out var handlers))
        {
            handlers = [];
            _handlers[typeof(T)] = handlers;
        }

        handlers.Add(handler);
        return new Unsubscriber(() => handlers.Remove(handler));
    }

    private sealed class Unsubscriber : IDisposable
    {
        private readonly Action _dispose;
        private bool _disposed;

        public Unsubscriber(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _dispose();
            _disposed = true;
        }
    }
}

