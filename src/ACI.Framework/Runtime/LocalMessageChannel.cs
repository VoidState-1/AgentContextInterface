namespace ACI.Framework.Runtime;

/// <summary>
/// 单 Agent 内的消息频道实现。
/// 维护本地订阅者映射。scope=Session 时委托给外部转发器处理跨 Agent 投递。
/// </summary>
public class LocalMessageChannel : IMessageChannel
{
    private readonly string _ownerAgentId;
    private readonly Dictionary<string, List<Action<ChannelMessage>>> _subscribers = [];
    private readonly object _lock = new();

    /// <summary>
    /// 跨 Agent 转发委托（由 Session 层注入）。
    /// 参数：(sourceAgentId, message)。
    /// </summary>
    private Action<string, ChannelMessage>? _crossAgentForwarder;

    public LocalMessageChannel(string ownerAgentId)
    {
        _ownerAgentId = ownerAgentId;
    }

    /// <summary>
    /// 设置跨 Agent 转发器（由 Session 在创建时调用）。
    /// </summary>
    public void SetForwarder(Action<string, ChannelMessage> forwarder)
    {
        _crossAgentForwarder = forwarder;
    }

    /// <inheritdoc />
    public void Post(string channel, string data, MessageScope scope = MessageScope.Local)
    {
        var message = new ChannelMessage
        {
            Channel = channel,
            Data = data,
            SourceAgentId = _ownerAgentId
        };

        // 1. 始终分发给本地订阅者
        DeliverLocal(message);

        // 2. scope=Session 时委托给转发器
        if (scope == MessageScope.Session)
        {
            _crossAgentForwarder?.Invoke(_ownerAgentId, message);
        }
    }

    /// <summary>
    /// 外部投递入口。由 Session 层的 ChannelBridge 调用，
    /// 把其他 Agent 的消息递送到本地订阅者。
    /// </summary>
    public void DeliverExternal(ChannelMessage message)
    {
        DeliverLocal(message);
    }

    /// <inheritdoc />
    public IDisposable Subscribe(string channel, Action<ChannelMessage> handler)
    {
        lock (_lock)
        {
            if (!_subscribers.TryGetValue(channel, out var list))
            {
                list = [];
                _subscribers[channel] = list;
            }
            list.Add(handler);
        }

        return new Unsubscriber(() =>
        {
            lock (_lock)
            {
                if (_subscribers.TryGetValue(channel, out var list))
                {
                    list.Remove(handler);
                }
            }
        });
    }

    /// <summary>
    /// 分发消息给本地订阅者。
    /// </summary>
    private void DeliverLocal(ChannelMessage message)
    {
        List<Action<ChannelMessage>>? handlers;
        lock (_lock)
        {
            if (!_subscribers.TryGetValue(message.Channel, out var list) || list.Count == 0)
                return;

            handlers = [.. list];
        }

        foreach (var handler in handlers)
        {
            try
            {
                handler(message);
            }
            catch
            {
                // 吞异常：防止一个订阅者崩溃影响其他订阅者。
            }
        }
    }

    /// <summary>
    /// 取消订阅的辅助类。
    /// </summary>
    private sealed class Unsubscriber(Action unsubscribe) : IDisposable
    {
        private Action? _unsubscribe = unsubscribe;

        public void Dispose()
        {
            _unsubscribe?.Invoke();
            _unsubscribe = null;
        }
    }
}
