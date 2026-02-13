using ACI.Core.Services;
using ACI.Framework.Runtime;
using ACI.LLM;
using ACI.LLM.Abstractions;
using ACI.Server.Services;
using ACI.Server.Settings;

namespace ACI.Server.Tests.Services;

public class SessionTests
{
    // 测试点：单 Agent Session 不应注册 MailboxApp。
    // 预期结果：Host 中不存在 mailbox 应用。
    [Fact]
    public void SingleAgent_ShouldNotRegisterMailbox()
    {
        var profiles = new List<AgentProfile>
        {
            new() { Id = "solo", Name = "Solo Agent" }
        };

        using var session = CreateSession(profiles);

        var agent = session.GetAgent("solo")!;
        Assert.False(agent.Host.IsStarted("mailbox"));
        // 尝试启动也不行，因为没注册
        var allApps = agent.Host.GetAllApps().Select(a => a.Name);
        Assert.DoesNotContain("mailbox", allApps);
    }

    // 测试点：多 Agent Session 应自动注册 MailboxApp。
    // 预期结果：每个 Agent 的 Host 中包含 mailbox 应用。
    [Fact]
    public void MultiAgent_ShouldRegisterMailbox()
    {
        var profiles = new List<AgentProfile>
        {
            new() { Id = "planner", Name = "Planner" },
            new() { Id = "coder", Name = "Coder" }
        };

        using var session = CreateSession(profiles);

        var plannerApps = session.GetAgent("planner")!.Host.GetAllApps().Select(a => a.Name);
        var coderApps = session.GetAgent("coder")!.Host.GetAllApps().Select(a => a.Name);

        Assert.Contains("mailbox", plannerApps);
        Assert.Contains("mailbox", coderApps);
    }

    // 测试点：频道桥接应将 scope=Session 消息转发给其他 Agent。
    // 预期结果：Agent-A Post 的消息被 Agent-B 的订阅者收到。
    [Fact]
    public void ChannelBridge_ShouldForwardSessionMessages()
    {
        var profiles = new List<AgentProfile>
        {
            new() { Id = "a", Name = "Agent A" },
            new() { Id = "b", Name = "Agent B" }
        };

        using var session = CreateSession(profiles);

        ChannelMessage? receivedByB = null;
        var agentA = session.GetAgent("a")!;
        var agentB = session.GetAgent("b")!;

        // Agent-B 订阅频道
        agentB.LocalMessageChannel.Subscribe("test.channel", msg => receivedByB = msg);

        // Agent-A 发送 scope=Session 消息
        agentA.LocalMessageChannel.Post("test.channel", "hello-from-a", MessageScope.Session);

        Assert.NotNull(receivedByB);
        Assert.Equal("hello-from-a", receivedByB!.Data);
        Assert.Equal("a", receivedByB.SourceAgentId);
    }

    // 测试点：scope=Local 消息不应跨 Agent 传递。
    // 预期结果：Agent-B 的订阅者不会收到 Agent-A 的 Local 消息。
    [Fact]
    public void ChannelBridge_LocalScope_ShouldNotForward()
    {
        var profiles = new List<AgentProfile>
        {
            new() { Id = "a", Name = "Agent A" },
            new() { Id = "b", Name = "Agent B" }
        };

        using var session = CreateSession(profiles);

        var receivedByB = false;
        session.GetAgent("b")!.LocalMessageChannel
            .Subscribe("local.ch", _ => receivedByB = true);

        session.GetAgent("a")!.LocalMessageChannel
            .Post("local.ch", "local-only", MessageScope.Local);

        Assert.False(receivedByB);
    }

    // 测试点：发送者本地也能收到自己的消息。
    // 预期结果：Agent-A 发送 scope=Session 后，自身的订阅者也收到消息。
    [Fact]
    public void ChannelBridge_SenderAlsoReceivesLocally()
    {
        var profiles = new List<AgentProfile>
        {
            new() { Id = "a", Name = "Agent A" },
            new() { Id = "b", Name = "Agent B" }
        };

        using var session = CreateSession(profiles);

        var receivedByA = false;
        session.GetAgent("a")!.LocalMessageChannel
            .Subscribe("echo", _ => receivedByA = true);

        session.GetAgent("a")!.LocalMessageChannel
            .Post("echo", "data", MessageScope.Session);

        Assert.True(receivedByA);
    }

    // 测试点：GetAgent 不存在的 ID 应返回 null。
    // 预期结果：返回 null。
    [Fact]
    public void GetAgent_NonExistent_ShouldReturnNull()
    {
        var profiles = new List<AgentProfile>
        {
            new() { Id = "a", Name = "Agent A" }
        };

        using var session = CreateSession(profiles);

        Assert.Null(session.GetAgent("nonexistent"));
    }

    // 测试点：GetAllAgents 应返回所有 Agent。
    // 预期结果：数量与配置一致。
    [Fact]
    public void GetAllAgents_ShouldReturnAll()
    {
        var profiles = new List<AgentProfile>
        {
            new() { Id = "a", Name = "Agent A" },
            new() { Id = "b", Name = "Agent B" },
            new() { Id = "c", Name = "Agent C" }
        };

        using var session = CreateSession(profiles);

        Assert.Equal(3, session.AgentCount);
        Assert.Equal(3, session.GetAllAgents().Count());
    }

    // 测试点：InteractAsync 对不存在的 Agent 应抛 InvalidOperationException。
    // 预期结果：抛出 InvalidOperationException。
    [Fact]
    public async Task InteractAsync_NonExistentAgent_ShouldThrow()
    {
        var profiles = new List<AgentProfile>
        {
            new() { Id = "a", Name = "Agent A" }
        };

        using var session = CreateSession(profiles);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.InteractAsync("missing", "hello"));
    }

    // 测试点：InteractAsync 应正确执行并返回结果。
    // 预期结果：Result.Success = true, Response 来自 LLM bridge。
    [Fact]
    public async Task InteractAsync_ShouldReturnLLMResult()
    {
        var profiles = new List<AgentProfile>
        {
            AgentProfile.Default()
        };

        using var session = CreateSession(profiles,
            new QueueLlmBridge([LLMResponse.Ok("test response")]));

        var result = await session.InteractAsync("default", "hello");

        Assert.True(result.Success);
        Assert.Equal("test response", result.Response);
    }

    // 测试点：Dispose 应释放所有 Agent。
    // 预期结果：重复 Dispose 不抛异常。
    [Fact]
    public void Dispose_ShouldBeIdempotent()
    {
        var profiles = new List<AgentProfile>
        {
            new() { Id = "a", Name = "Agent A" }
        };

        var session = CreateSession(profiles);

        var ex = Record.Exception(() =>
        {
            session.Dispose();
            session.Dispose(); // 重复 Dispose
        });

        Assert.Null(ex);
    }

    private static Session CreateSession(
        IReadOnlyList<AgentProfile> profiles,
        ILLMBridge? llmBridge = null)
    {
        return new Session(
            Guid.NewGuid().ToString("N"),
            profiles,
            llmBridge ?? new QueueLlmBridge([]),
            new ACIOptions
            {
                Render = new ContextRenderOptions
                {
                    MaxTokens = 4000,
                    MinConversationTokens = 1000,
                    PruneTargetTokens = 2000
                }
            });
    }

    private sealed class QueueLlmBridge : ILLMBridge
    {
        private readonly Queue<LLMResponse> _responses;

        public QueueLlmBridge(IEnumerable<LLMResponse> responses)
        {
            _responses = new Queue<LLMResponse>(responses);
        }

        public Task<LLMResponse> SendAsync(IEnumerable<LlmMessage> messages, CancellationToken ct = default)
        {
            return _responses.Count == 0
                ? Task.FromResult(LLMResponse.Fail("no queued response"))
                : Task.FromResult(_responses.Dequeue());
        }
    }
}
