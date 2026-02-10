using ACI.Core.Models;
using ACI.LLM.Abstractions;
using ACI.Server.Hubs;
using ACI.Server.Services;
using ACI.Server.Settings;

namespace ACI.Server.Tests.Services;

public class SessionManagerTests
{
    // 测试点：CreateSession 应创建会话并触发 Created 事件。
    // 预期结果：可通过 sessionId 取回会话，事件类型为 Created。
    [Fact]
    public void CreateSession_ShouldTrackSessionAndRaiseCreatedEvent()
    {
        var manager = CreateManager(out _);
        SessionChangeEvent? changeEvent = null;
        manager.OnSessionChange += evt => changeEvent = evt;

        var session = manager.CreateSession("session-a");

        Assert.NotNull(manager.GetSession("session-a"));
        Assert.Contains("session-a", manager.GetActiveSessions());
        Assert.NotNull(changeEvent);
        Assert.Equal("session-a", changeEvent!.SessionId);
        Assert.Equal(SessionChangeType.Created, changeEvent.Type);

        manager.CloseSession(session.SessionId);
    }

    // 测试点：CloseSession 应移除会话并触发 Closed 事件。
    // 预期结果：会话不可再获取，事件类型为 Closed。
    [Fact]
    public void CloseSession_ShouldRemoveSessionAndRaiseClosedEvent()
    {
        var manager = CreateManager(out _);
        var session = manager.CreateSession("session-b");

        SessionChangeEvent? closedEvent = null;
        manager.OnSessionChange += evt =>
        {
            if (evt.Type == SessionChangeType.Closed)
            {
                closedEvent = evt;
            }
        };

        manager.CloseSession("session-b");

        Assert.Null(manager.GetSession("session-b"));
        Assert.DoesNotContain("session-b", manager.GetActiveSessions());
        Assert.NotNull(closedEvent);
        Assert.Equal("session-b", closedEvent!.SessionId);
    }

    // 测试点：窗口创建/更新/关闭事件应通过 SessionManager 转发给 Hub 通知器。
    // 预期结果：通知器收到对应的 created/updated/closed 调用。
    [Fact]
    public async Task SessionWindowChanges_ShouldNotifyHub()
    {
        var manager = CreateManager(out var notifier);
        var session = manager.CreateSession("session-c");

        var launched = session.Host.Launch("file_explorer");
        session.Host.RefreshWindow(launched.Id);
        session.Windows.Remove(launched.Id);

        await WaitForAsync(() => notifier.Created.Any(w => w.Id == launched.Id));
        await WaitForAsync(() => notifier.Updated.Any(w => w.Id == launched.Id));
        await WaitForAsync(() => notifier.Closed.Contains(launched.Id));

        Assert.Contains(notifier.Created, w => w.Id == launched.Id);
        Assert.Contains(notifier.Updated, w => w.Id == launched.Id);
        Assert.Contains(launched.Id, notifier.Closed);

        manager.CloseSession("session-c");
    }

    // 测试点：关闭不存在会话应安全无副作用。
    // 预期结果：不抛异常，活跃会话集合不变。
    [Fact]
    public void CloseSession_MissingSession_ShouldBeNoOp()
    {
        var manager = CreateManager(out _);
        manager.CreateSession("session-d");

        var ex = Record.Exception(() => manager.CloseSession("missing"));

        Assert.Null(ex);
        Assert.Contains("session-d", manager.GetActiveSessions());

        manager.CloseSession("session-d");
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

    private sealed class SpyHubNotifier : IACIHubNotifier
    {
        public List<Window> Created { get; } = [];
        public List<Window> Updated { get; } = [];
        public List<string> Closed { get; } = [];

        public Task NotifyWindowCreated(string sessionId, Window window)
        {
            Created.Add(window);
            return Task.CompletedTask;
        }

        public Task NotifyWindowUpdated(string sessionId, Window window)
        {
            Updated.Add(window);
            return Task.CompletedTask;
        }

        public Task NotifyWindowClosed(string sessionId, string windowId)
        {
            Closed.Add(windowId);
            return Task.CompletedTask;
        }
    }
}
