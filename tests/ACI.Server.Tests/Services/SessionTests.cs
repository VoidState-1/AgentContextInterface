using ACI.Core.Services;
using ACI.Framework.Runtime;
using ACI.LLM;
using ACI.LLM.Abstractions;
using ACI.Server.Services;
using ACI.Server.Settings;

namespace ACI.Server.Tests.Services;

public class SessionTests
{
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
        var allApps = agent.Host.GetAllApps().Select(a => a.Name);
        Assert.DoesNotContain("mailbox", allApps);
    }

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

    [Fact]
    public async Task ChannelBridge_ShouldForwardSessionMessages()
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

        agentB.LocalMessageChannel.Subscribe("test.channel", msg => receivedByB = msg);
        agentA.LocalMessageChannel.Post("test.channel", "hello-from-a", MessageScope.Session);
        Assert.Null(receivedByB);

        await session.SimulateAsync("a", "drain wakeup queue");

        Assert.NotNull(receivedByB);
        Assert.Equal("hello-from-a", receivedByB!.Data);
        Assert.Equal("a", receivedByB.SourceAgentId);
    }

    [Fact]
    public async Task ChannelBridge_LocalScope_ShouldNotForward()
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

        await session.SimulateAsync("a", "drain wakeup queue");

        Assert.False(receivedByB);
    }

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

    [Fact]
    public async Task ChannelBridge_TargetedMessage_ShouldOnlyReachTargetAgent()
    {
        var profiles = new List<AgentProfile>
        {
            new() { Id = "a", Name = "Agent A" },
            new() { Id = "b", Name = "Agent B" },
            new() { Id = "c", Name = "Agent C" }
        };

        using var session = CreateSession(profiles);

        ChannelMessage? receivedByB = null;
        ChannelMessage? receivedByC = null;
        session.GetAgent("b")!.LocalMessageChannel.Subscribe("target.ch", msg => receivedByB = msg);
        session.GetAgent("c")!.LocalMessageChannel.Subscribe("target.ch", msg => receivedByC = msg);

        session.GetAgent("a")!.LocalMessageChannel.Post(
            "target.ch",
            "target-only",
            MessageScope.Session,
            ["b"]);

        await session.SimulateAsync("a", "drain wakeup queue");

        Assert.NotNull(receivedByB);
        Assert.Null(receivedByC);
    }

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
            session.Dispose();
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
