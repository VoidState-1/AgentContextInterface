using ACI.Core.Models;
using ACI.LLM.Abstractions;
using ACI.Server.Dto;
using ACI.Server.Hubs;
using ACI.Server.Services;
using ACI.Server.Settings;

namespace ACI.Server.Tests.Services;

public class SessionManagerTests
{
    // 测试点：不带参数 CreateSession 应创建默认单 Agent 会话。
    // 预期结果：可通过返回的 sessionId 取回会话，事件类型为 Created，包含 1 个 Agent。
    [Fact]
    public void CreateSession_Default_ShouldCreateSingleAgentSession()
    {
        var manager = CreateManager(out _);
        SessionChangeEvent? changeEvent = null;
        manager.OnSessionChange += evt => changeEvent = evt;

        var session = manager.CreateSession();

        Assert.NotNull(manager.GetSession(session.SessionId));
        Assert.Contains(session.SessionId, manager.GetActiveSessions());
        Assert.Equal(1, session.AgentCount);
        Assert.NotNull(changeEvent);
        Assert.Equal(SessionChangeType.Created, changeEvent!.Type);

        manager.CloseSession(session.SessionId);
    }

    // 测试点：传入多 Agent 配置创建 Session。
    // 预期结果：Session 包含指定数量的 Agent，且可通过 ID 找到。
    [Fact]
    public void CreateSession_MultiAgent_ShouldCreateAllAgents()
    {
        var manager = CreateManager(out _);
        var request = new CreateSessionRequest
        {
            Agents =
            [
                new AgentProfileDto { Id = "planner", Name = "Planner" },
                new AgentProfileDto { Id = "coder", Name = "Coder" }
            ]
        };

        var session = manager.CreateSession(request);

        Assert.Equal(2, session.AgentCount);
        Assert.NotNull(session.GetAgent("planner"));
        Assert.NotNull(session.GetAgent("coder"));

        manager.CloseSession(session.SessionId);
    }

    // 测试点：CloseSession 应移除会话并触发 Closed 事件。
    // 预期结果：会话不可再获取，事件类型为 Closed。
    [Fact]
    public void CloseSession_ShouldRemoveSessionAndRaiseClosedEvent()
    {
        var manager = CreateManager(out _);
        var session = manager.CreateSession();

        SessionChangeEvent? closedEvent = null;
        manager.OnSessionChange += evt =>
        {
            if (evt.Type == SessionChangeType.Closed)
            {
                closedEvent = evt;
            }
        };

        manager.CloseSession(session.SessionId);

        Assert.Null(manager.GetSession(session.SessionId));
        Assert.DoesNotContain(session.SessionId, manager.GetActiveSessions());
        Assert.NotNull(closedEvent);
        Assert.Equal(session.SessionId, closedEvent!.SessionId);
    }

    // 测试点：窗口创建/更新/关闭事件应通过 SessionManager 转发给 Hub 通知器。
    // 预期结果：通知器收到对应的 created/updated/closed 调用，且携带 agentId。
    [Fact]
    public async Task SessionWindowChanges_ShouldNotifyHubWithAgentId()
    {
        var manager = CreateManager(out var notifier);
        var session = manager.CreateSession();
        var agent = session.GetAllAgents().First();

        var launched = agent.Host.Launch("file_explorer");
        agent.Host.RefreshWindow(launched.Id);
        agent.Windows.Remove(launched.Id);

        await WaitForAsync(() => notifier.Created.Any(e => e.WindowId == launched.Id));
        await WaitForAsync(() => notifier.Updated.Any(e => e.WindowId == launched.Id));
        await WaitForAsync(() => notifier.Closed.Any(e => e.WindowId == launched.Id));

        // 验证 agentId 被传递
        Assert.All(notifier.Created, e => Assert.Equal(agent.AgentId, e.AgentId));
        Assert.All(notifier.Updated, e => Assert.Equal(agent.AgentId, e.AgentId));
        Assert.All(notifier.Closed, e => Assert.Equal(agent.AgentId, e.AgentId));

        manager.CloseSession(session.SessionId);
    }

    // 测试点：关闭不存在会话应安全无副作用。
    // 预期结果：不抛异常。
    [Fact]
    public void CloseSession_MissingSession_ShouldBeNoOp()
    {
        var manager = CreateManager(out _);
        var session = manager.CreateSession();

        var ex = Record.Exception(() => manager.CloseSession("missing"));
        Assert.Null(ex);

        manager.CloseSession(session.SessionId);
    }

    [Fact]
    public void CreateSession_DuplicateAgentIds_ShouldThrow()
    {
        var manager = CreateManager(out _);
        var request = new CreateSessionRequest
        {
            Agents =
            [
                new AgentProfileDto { Id = "planner", Name = "Planner A" },
                new AgentProfileDto { Id = "planner", Name = "Planner B" }
            ]
        };

        var ex = Assert.Throws<ArgumentException>(() => manager.CreateSession(request));
        Assert.Contains("Duplicate agent id", ex.Message);
    }

    [Fact]
    public void CreateSession_InvalidAgentId_ShouldThrow()
    {
        var manager = CreateManager(out _);
        var request = new CreateSessionRequest
        {
            Agents =
            [
                new AgentProfileDto { Id = "invalid id", Name = "Planner" }
            ]
        };

        var ex = Assert.Throws<ArgumentException>(() => manager.CreateSession(request));
        Assert.Contains("Invalid agent id", ex.Message);
    }

    private static SessionManager CreateManager(out SpyHubNotifier notifier)
    {
        notifier = new SpyHubNotifier();
        return new SessionManager(
            llmBridge: new FakeLlmBridge(),
            hubNotifier: notifier,
            options: new ACIOptions
            {
                Render = new ContextRenderOptions
                {
                    MaxTokens = 4000,
                    MinConversationTokens = 1000,
                    PruneTargetTokens = 2000
                }
            });
    }

    private static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var start = Environment.TickCount64;
        while (!condition())
        {
            if (Environment.TickCount64 - start > timeoutMs)
            {
                throw new TimeoutException("Timed out waiting for expected notification.");
            }

            await Task.Delay(20);
        }
    }

    private sealed class FakeLlmBridge : ILLMBridge
    {
        public Task<LLMResponse> SendAsync(IEnumerable<ACI.Core.Services.LlmMessage> messages, CancellationToken ct = default)
        {
            return Task.FromResult(LLMResponse.Ok("ok"));
        }
    }

    private sealed record WindowNotification(string AgentId, string WindowId);

    private sealed class SpyHubNotifier : IACIHubNotifier
    {
        public List<WindowNotification> Created { get; } = [];
        public List<WindowNotification> Updated { get; } = [];
        public List<WindowNotification> Closed { get; } = [];

        public Task NotifyWindowCreated(string sessionId, string agentId, Window window)
        {
            Created.Add(new WindowNotification(agentId, window.Id));
            return Task.CompletedTask;
        }

        public Task NotifyWindowUpdated(string sessionId, string agentId, Window window)
        {
            Updated.Add(new WindowNotification(agentId, window.Id));
            return Task.CompletedTask;
        }

        public Task NotifyWindowClosed(string sessionId, string agentId, string windowId)
        {
            Closed.Add(new WindowNotification(agentId, windowId));
            return Task.CompletedTask;
        }
    }
}
