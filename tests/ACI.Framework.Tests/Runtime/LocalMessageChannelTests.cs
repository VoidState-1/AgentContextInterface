using ACI.Framework.Runtime;

namespace ACI.Framework.Tests.Runtime;

public class LocalMessageChannelTests
{
    // 测试点：Post scope=Local，验证本地订阅者收到消息。
    // 预期结果：订阅者回调被触发，消息内容和 sourceAgentId 正确。
    [Fact]
    public void Post_Local_ShouldDeliverToLocalSubscribers()
    {
        var channel = new LocalMessageChannel("agent-a");
        ChannelMessage? received = null;

        channel.Subscribe("test.channel", msg => received = msg);
        channel.Post("test.channel", """{"key":"value"}""", MessageScope.Local);

        Assert.NotNull(received);
        Assert.Equal("test.channel", received!.Channel);
        Assert.Equal("""{"key":"value"}""", received.Data);
        Assert.Equal("agent-a", received.SourceAgentId);
    }

    // 测试点：Post scope=Local 不应调用跨 Agent 转发器。
    // 预期结果：转发器委托未被调用。
    [Fact]
    public void Post_Local_ShouldNotCallForwarder()
    {
        var channel = new LocalMessageChannel("agent-a");
        var forwarderCalled = false;

        channel.SetForwarder((_, _) => forwarderCalled = true);
        channel.Subscribe("test.channel", _ => { }); // 需要订阅者
        channel.Post("test.channel", "data", MessageScope.Local);

        Assert.False(forwarderCalled);
    }

    // 测试点：Post scope=Session 应同时分发给本地订阅者并调用转发器。
    // 预期结果：本地订阅者收到消息，且转发器被调用。
    [Fact]
    public void Post_Session_ShouldDeliverLocallyAndCallForwarder()
    {
        var channel = new LocalMessageChannel("agent-a");
        var localReceived = false;
        var forwarderCalled = false;
        string? forwardedAgentId = null;
        ChannelMessage? forwardedMessage = null;

        channel.Subscribe("chat", _ => localReceived = true);
        channel.SetForwarder((agentId, msg) =>
        {
            forwarderCalled = true;
            forwardedAgentId = agentId;
            forwardedMessage = msg;
        });

        channel.Post("chat", "hello", MessageScope.Session);

        Assert.True(localReceived);
        Assert.True(forwarderCalled);
        Assert.Equal("agent-a", forwardedAgentId);
        Assert.NotNull(forwardedMessage);
        Assert.Equal("chat", forwardedMessage!.Channel);
        Assert.Equal("hello", forwardedMessage.Data);
    }

    // 测试点：Post scope=Session 但未设置转发器，不应抛异常。
    // 预期结果：本地订阅者正常收到消息。
    [Fact]
    public void Post_Session_WithoutForwarder_ShouldStillDeliverLocally()
    {
        var channel = new LocalMessageChannel("agent-a");
        var received = false;

        channel.Subscribe("ch", _ => received = true);
        channel.Post("ch", "data", MessageScope.Session);

        Assert.True(received);
    }

    // 测试点：订阅者 Dispose 后不再收到消息。
    // 预期结果：Dispose 后新的 Post 不触发已取消的订阅者。
    [Fact]
    public void Subscribe_Dispose_ShouldUnsubscribe()
    {
        var channel = new LocalMessageChannel("agent-a");
        var callCount = 0;

        var subscription = channel.Subscribe("events", _ => callCount++);
        channel.Post("events", "first");
        Assert.Equal(1, callCount);

        subscription.Dispose();
        channel.Post("events", "second");
        Assert.Equal(1, callCount); // 不再增加
    }

    // 测试点：多个频道互不干扰。
    // 预期结果：频道 A 的消息不会被频道 B 的订阅者收到。
    [Fact]
    public void Subscribe_DifferentChannels_ShouldNotCrossTalk()
    {
        var channel = new LocalMessageChannel("agent-a");
        var aReceived = false;
        var bReceived = false;

        channel.Subscribe("channel-a", _ => aReceived = true);
        channel.Subscribe("channel-b", _ => bReceived = true);

        channel.Post("channel-a", "only-a");

        Assert.True(aReceived);
        Assert.False(bReceived);
    }

    // 测试点：DeliverExternal 应分发给本地订阅者。
    // 预期结果：外部投递的消息触发本地订阅者。
    [Fact]
    public void DeliverExternal_ShouldDeliverToLocalSubscribers()
    {
        var channel = new LocalMessageChannel("agent-b");
        ChannelMessage? received = null;

        channel.Subscribe("mail", msg => received = msg);

        var externalMsg = new ChannelMessage
        {
            Channel = "mail",
            Data = "external data",
            SourceAgentId = "agent-a"
        };
        channel.DeliverExternal(externalMsg);

        Assert.NotNull(received);
        Assert.Equal("agent-a", received!.SourceAgentId);
        Assert.Equal("external data", received.Data);
    }

    // 测试点：订阅者抛异常不应影响其他订阅者。
    // 预期结果：第一个订阅者抛异常，第二个订阅者仍然收到消息。
    [Fact]
    public void Post_SubscriberThrows_ShouldNotAffectOthers()
    {
        var channel = new LocalMessageChannel("agent-a");
        var secondReceived = false;

        channel.Subscribe("ch", _ => throw new InvalidOperationException("boom"));
        channel.Subscribe("ch", _ => secondReceived = true);

        channel.Post("ch", "data");

        Assert.True(secondReceived);
    }

    // 测试点：同一频道多个订阅者都应收到消息。
    // 预期结果：所有订阅者的回调均被触发。
    [Fact]
    public void Post_MultipleSubscribers_ShouldDeliverToAll()
    {
        var channel = new LocalMessageChannel("agent-a");
        var count = 0;

        channel.Subscribe("ch", _ => count++);
        channel.Subscribe("ch", _ => count++);
        channel.Subscribe("ch", _ => count++);

        channel.Post("ch", "data");

        Assert.Equal(3, count);
    }
}
