using ACI.Core.Abstractions;
using ACI.Core.Services;

namespace ACI.Core.Tests.Services;

public class EventBusTests
{
    // 测试点：订阅后发布事件应触发对应处理器。
    // 预期结果：处理器被调用一次，收到正确数据。
    [Fact]
    public void Publish_AfterSubscribe_ShouldInvokeHandler()
    {
        var bus = new EventBus();
        var received = new List<int>();
        bus.Subscribe<TestEvent>(evt => received.Add(evt.Value));

        bus.Publish(new TestEvent(7));

        Assert.Equal([7], received);
    }

    // 测试点：取消订阅后不应再接收事件。
    // 预期结果：处理器只在取消前被调用。
    [Fact]
    public void Publish_AfterDisposeSubscription_ShouldNotInvokeHandler()
    {
        var bus = new EventBus();
        var received = new List<int>();
        var sub = bus.Subscribe<TestEvent>(evt => received.Add(evt.Value));

        bus.Publish(new TestEvent(1));
        sub.Dispose();
        bus.Publish(new TestEvent(2));

        Assert.Equal([1], received);
    }

    // 测试点：某个处理器抛异常时，不应影响其他处理器执行。
    // 预期结果：后续处理器仍被调用且 Publish 不抛出异常。
    [Fact]
    public void Publish_HandlerThrows_ShouldNotBreakOtherHandlers()
    {
        var bus = new EventBus();
        var called = 0;
        bus.Subscribe<TestEvent>(_ => throw new InvalidOperationException("boom"));
        bus.Subscribe<TestEvent>(_ => called++);

        var exception = Record.Exception(() => bus.Publish(new TestEvent(9)));

        Assert.Null(exception);
        Assert.Equal(1, called);
    }

    private sealed record TestEvent(int Value) : IEvent;
}

