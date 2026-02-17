using ACI.Core.Models;
using ACI.LLM.Abstractions;
using ACI.Server.Dto;
using ACI.Server.Hubs;
using ACI.Server.Services;
using ACI.Server.Settings;
using ACI.Storage;

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

    [Fact]
    public async Task SaveSessionAsync_ExistingSession_ShouldPersistSnapshot()
    {
        var manager = CreateManager(out _, out var store);
        var session = manager.CreateSession();

        var saved = await manager.SaveSessionAsync(session.SessionId);
        var exists = await store.ExistsAsync(session.SessionId);

        Assert.True(saved);
        Assert.True(exists);

        manager.CloseSession(session.SessionId);
    }

    [Fact]
    public async Task SaveSessionAsync_MissingSession_ShouldReturnFalse()
    {
        var manager = CreateManager(out _, out _);

        var saved = await manager.SaveSessionAsync("missing");

        Assert.False(saved);
    }

    [Fact]
    public async Task LoadSessionAsync_SavedSession_ShouldRestoreToActiveSessions()
    {
        var manager = CreateManager(out _, out var store);
        var session = manager.CreateSession();
        await manager.SaveSessionAsync(session.SessionId);
        manager.CloseSession(session.SessionId);
        Assert.Null(manager.GetSession(session.SessionId));

        var loaded = await manager.LoadSessionAsync(session.SessionId);

        Assert.NotNull(loaded);
        Assert.NotNull(manager.GetSession(session.SessionId));
        Assert.Equal(loaded!.SessionId, session.SessionId);
        Assert.True(await store.ExistsAsync(session.SessionId));

        manager.CloseSession(session.SessionId);
    }

    [Fact]
    public async Task ListAndDeleteSavedSessions_ShouldReflectStoreState()
    {
        var manager = CreateManager(out _, out _);
        var session = manager.CreateSession();
        await manager.SaveSessionAsync(session.SessionId);

        var list = await manager.ListSavedSessionsAsync();
        Assert.Contains(list, s => s.SessionId == session.SessionId);

        var deleted = await manager.DeleteSavedSessionAsync(session.SessionId);
        var listAfterDelete = await manager.ListSavedSessionsAsync();

        Assert.True(deleted);
        Assert.DoesNotContain(listAfterDelete, s => s.SessionId == session.SessionId);

        manager.CloseSession(session.SessionId);
    }

    [Fact]
    public async Task RequestAutoSave_ExistingSession_ShouldPersistAfterDebounce()
    {
        var manager = CreateManager(
            out _,
            out var store,
            new ACIOptions
            {
                Render = new ContextRenderOptions
                {
                    MaxTokens = 4000,
                    MinConversationTokens = 1000,
                    PruneTargetTokens = 2000
                },
                Persistence = new PersistenceOptions
                {
                    AutoSave = new AutoSaveOptions
                    {
                        Enabled = true,
                        DebounceMilliseconds = 30
                    }
                }
            });
        var session = manager.CreateSession();

        manager.RequestAutoSave(session.SessionId);
        await WaitForAsync(() => store.GetSaveCount(session.SessionId) >= 1);

        Assert.True(await store.ExistsAsync(session.SessionId));
        manager.CloseSession(session.SessionId);
    }

    [Fact]
    public async Task RequestAutoSave_Debounce_ShouldCoalesceToSingleSave()
    {
        var manager = CreateManager(
            out _,
            out var store,
            new ACIOptions
            {
                Render = new ContextRenderOptions
                {
                    MaxTokens = 4000,
                    MinConversationTokens = 1000,
                    PruneTargetTokens = 2000
                },
                Persistence = new PersistenceOptions
                {
                    AutoSave = new AutoSaveOptions
                    {
                        Enabled = true,
                        DebounceMilliseconds = 80
                    }
                }
            });
        var session = manager.CreateSession();

        manager.RequestAutoSave(session.SessionId);
        manager.RequestAutoSave(session.SessionId);
        manager.RequestAutoSave(session.SessionId);

        await WaitForAsync(() => store.GetSaveCount(session.SessionId) >= 1);
        await Task.Delay(180);

        Assert.Equal(1, store.GetSaveCount(session.SessionId));
        manager.CloseSession(session.SessionId);
    }

    // 测试点：关闭自动保存开关后，请求自动保存不应写入快照。
    // 预期结果：saveCount 保持 0，存储中不存在对应会话。
    [Fact]
    public async Task RequestAutoSave_Disabled_ShouldSkipSave()
    {
        var manager = CreateManager(
            out _,
            out var store,
            new ACIOptions
            {
                Render = new ContextRenderOptions
                {
                    MaxTokens = 4000,
                    MinConversationTokens = 1000,
                    PruneTargetTokens = 2000
                },
                Persistence = new PersistenceOptions
                {
                    AutoSave = new AutoSaveOptions
                    {
                        Enabled = false,
                        DebounceMilliseconds = 10
                    }
                }
            });
        var session = manager.CreateSession();

        manager.RequestAutoSave(session.SessionId);
        await Task.Delay(80);

        Assert.Equal(0, store.GetSaveCount(session.SessionId));
        Assert.False(await store.ExistsAsync(session.SessionId));
        manager.CloseSession(session.SessionId);
    }

    // 测试点：会话关闭前存在待执行自动保存时，应取消该保存任务。
    // 预期结果：关闭后不发生保存，saveCount 保持 0。
    [Fact]
    public async Task RequestAutoSave_CloseSessionBeforeDebounce_ShouldCancelPendingSave()
    {
        var manager = CreateManager(
            out _,
            out var store,
            new ACIOptions
            {
                Render = new ContextRenderOptions
                {
                    MaxTokens = 4000,
                    MinConversationTokens = 1000,
                    PruneTargetTokens = 2000
                },
                Persistence = new PersistenceOptions
                {
                    AutoSave = new AutoSaveOptions
                    {
                        Enabled = true,
                        DebounceMilliseconds = 200
                    }
                }
            });
        var session = manager.CreateSession();

        manager.RequestAutoSave(session.SessionId);
        manager.CloseSession(session.SessionId);
        await Task.Delay(260);

        Assert.Equal(0, store.GetSaveCount(session.SessionId));
        Assert.False(await store.ExistsAsync(session.SessionId));
    }

    // 测试点：对不存在会话调用自动保存请求应直接忽略。
    // 预期结果：不会触发任何保存调用。
    [Fact]
    public async Task RequestAutoSave_MissingSession_ShouldNoOp()
    {
        var manager = CreateManager(out _, out var store);

        manager.RequestAutoSave("missing-session");
        await Task.Delay(80);

        Assert.Equal(0, store.GetTotalSaveCount());
    }

    [Fact]
    public async Task LoadSessionAsync_UnsupportedSnapshotVersion_ShouldThrow()
    {
        var manager = CreateManager(out _, out var store);
        var session = manager.CreateSession();
        await manager.SaveSessionAsync(session.SessionId);
        manager.CloseSession(session.SessionId);

        var snapshot = await store.LoadAsync(session.SessionId);
        Assert.NotNull(snapshot);
        snapshot!.Version = Session.SnapshotVersion + 1;
        await store.SaveAsync(snapshot);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.LoadSessionAsync(session.SessionId));
    }

    // 测试点：删除不存在的已保存会话应返回 false。
    // 预期结果：不抛异常，返回 false。
    [Fact]
    public async Task DeleteSavedSessionAsync_Missing_ShouldReturnFalse()
    {
        var manager = CreateManager(out _, out _);

        var deleted = await manager.DeleteSavedSessionAsync("missing");

        Assert.False(deleted);
    }

    [Fact]
    public async Task SimulateAsync_ShouldTriggerAutoSaveViaSessionMutationHook()
    {
        var manager = CreateManager(
            out _,
            out var store,
            new ACIOptions
            {
                Render = new ContextRenderOptions
                {
                    MaxTokens = 4000,
                    MinConversationTokens = 1000,
                    PruneTargetTokens = 2000
                },
                Persistence = new PersistenceOptions
                {
                    AutoSave = new AutoSaveOptions
                    {
                        Enabled = true,
                        DebounceMilliseconds = 20
                    }
                }
            });
        var session = manager.CreateSession();

        var result = await session.SimulateAsync("default", "assistant simulated output");
        Assert.True(result.Success);

        await WaitForAsync(() => store.GetSaveCount(session.SessionId) >= 1);
        manager.CloseSession(session.SessionId);
    }

    [Fact]
    public async Task ExecuteWindowActionAsync_ShouldTriggerAutoSaveViaSessionMutationHook()
    {
        var manager = CreateManager(
            out _,
            out var store,
            new ACIOptions
            {
                Render = new ContextRenderOptions
                {
                    MaxTokens = 4000,
                    MinConversationTokens = 1000,
                    PruneTargetTokens = 2000
                },
                Persistence = new PersistenceOptions
                {
                    AutoSave = new AutoSaveOptions
                    {
                        Enabled = true,
                        DebounceMilliseconds = 20
                    }
                }
            });
        var session = manager.CreateSession();

        var actionResult = await session.ExecuteWindowActionAsync(
            "default",
            "missing_window",
            "any_action");
        Assert.False(actionResult.Success);

        await WaitForAsync(() => store.GetSaveCount(session.SessionId) >= 1);
        manager.CloseSession(session.SessionId);
    }

    private static SessionManager CreateManager(
        out SpyHubNotifier notifier,
        out InMemorySessionStore store,
        ACIOptions? options = null)
    {
        notifier = new SpyHubNotifier();
        store = new InMemorySessionStore();
        return new SessionManager(
            llmBridge: new FakeLlmBridge(),
            hubNotifier: notifier,
            store: store,
            options: options ?? new ACIOptions
            {
                Render = new ContextRenderOptions
                {
                    MaxTokens = 4000,
                    MinConversationTokens = 1000,
                    PruneTargetTokens = 2000
                }
            });
    }

    private static SessionManager CreateManager(out SpyHubNotifier notifier)
    {
        return CreateManager(out notifier, out _);
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

    private sealed class InMemorySessionStore : ISessionStore
    {
        private readonly Dictionary<string, SessionSnapshot> _snapshots = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _saveCounts = new(StringComparer.OrdinalIgnoreCase);

        public Task SaveAsync(SessionSnapshot snapshot, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            _snapshots[snapshot.SessionId] = snapshot;
            if (_saveCounts.TryGetValue(snapshot.SessionId, out var count))
            {
                _saveCounts[snapshot.SessionId] = count + 1;
            }
            else
            {
                _saveCounts[snapshot.SessionId] = 1;
            }
            return Task.CompletedTask;
        }

        public Task<SessionSnapshot?> LoadAsync(string sessionId, CancellationToken ct = default)
        {
            _snapshots.TryGetValue(sessionId, out var snapshot);
            return Task.FromResult(snapshot);
        }

        public Task DeleteAsync(string sessionId, CancellationToken ct = default)
        {
            _snapshots.Remove(sessionId);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SessionSummary>> ListAsync(CancellationToken ct = default)
        {
            var list = _snapshots.Values
                .Select(snapshot => new SessionSummary
                {
                    SessionId = snapshot.SessionId,
                    CreatedAt = snapshot.CreatedAt,
                    SnapshotAt = snapshot.SnapshotAt,
                    AgentCount = snapshot.Agents.Count
                })
                .OrderByDescending(s => s.SnapshotAt)
                .ToList();
            return Task.FromResult<IReadOnlyList<SessionSummary>>(list);
        }

        public Task<bool> ExistsAsync(string sessionId, CancellationToken ct = default)
        {
            return Task.FromResult(_snapshots.ContainsKey(sessionId));
        }

        public int GetSaveCount(string sessionId)
        {
            return _saveCounts.GetValueOrDefault(sessionId);
        }

        public int GetTotalSaveCount()
        {
            return _saveCounts.Values.Sum();
        }
    }
}
