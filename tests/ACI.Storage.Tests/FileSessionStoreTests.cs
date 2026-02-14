using System.Text.Json;
using ACI.Core.Models;
using ACI.Framework.Runtime;
using ACI.Storage;

namespace ACI.Storage.Tests;

public class FileSessionStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileSessionStore _store;

    public FileSessionStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "aci_storage_test_" + Guid.NewGuid().ToString("N"));
        _store = new FileSessionStore(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private static SessionSnapshot CreateTestSnapshot(string sessionId = "test-session-1")
    {
        return new SessionSnapshot
        {
            SessionId = sessionId,
            CreatedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            SnapshotAt = DateTime.UtcNow,
            Version = 1,
            Agents =
            [
                new AgentSnapshot
                {
                    Profile = new AgentProfileSnapshot
                    {
                        Id = "agent-1",
                        Name = "TestAgent",
                        Role = "assistant",
                        MaxTokenBudget = 8000
                    },
                    ClockSeq = 42,
                    ContextItems =
                    [
                        new ContextItemSnapshot
                        {
                            Id = "ctx-1",
                            Type = ContextItemType.User,
                            Seq = 1,
                            Content = "Hello",
                            IsObsolete = false,
                            EstimatedTokens = 5
                        },
                        new ContextItemSnapshot
                        {
                            Id = "ctx-2",
                            Type = ContextItemType.Assistant,
                            Seq = 2,
                            Content = "Hello! How can I help?",
                            IsObsolete = false,
                            EstimatedTokens = 10
                        }
                    ],
                    Apps =
                    [
                        new AppSnapshot
                        {
                            Name = "test_app",
                            IsStarted = true,
                            ManagedWindowIds = ["win-1", "win-2"],
                            StateData = new Dictionary<string, JsonElement>
                            {
                                ["counter"] = JsonSerializer.SerializeToElement(42),
                                ["name"] = JsonSerializer.SerializeToElement("test")
                            }
                        }
                    ]
                }
            ]
        };
    }

    // ===== Save & Load =====

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesData()
    {
        var original = CreateTestSnapshot();

        await _store.SaveAsync(original);
        var loaded = await _store.LoadAsync("test-session-1");

        Assert.NotNull(loaded);
        Assert.Equal(original.SessionId, loaded.SessionId);
        Assert.Equal(original.CreatedAt, loaded.CreatedAt);
        Assert.Equal(original.Version, loaded.Version);
        Assert.Single(loaded.Agents);

        var agent = loaded.Agents[0];
        Assert.Equal("agent-1", agent.Profile.Id);
        Assert.Equal("TestAgent", agent.Profile.Name);
        Assert.Equal(42, agent.ClockSeq);
        Assert.Equal(2, agent.ContextItems.Count);
        Assert.Equal("Hello", agent.ContextItems[0].Content);
        Assert.Equal(ContextItemType.User, agent.ContextItems[0].Type);
        Assert.Single(agent.Apps);
        Assert.Equal("test_app", agent.Apps[0].Name);
        Assert.True(agent.Apps[0].IsStarted);
    }

    [Fact]
    public async Task Save_OverwritesExistingSnapshot()
    {
        var v1 = CreateTestSnapshot();
        await _store.SaveAsync(v1);

        var v2 = CreateTestSnapshot();
        v2.Agents[0].ClockSeq = 100;
        await _store.SaveAsync(v2);

        var loaded = await _store.LoadAsync("test-session-1");
        Assert.NotNull(loaded);
        Assert.Equal(100, loaded.Agents[0].ClockSeq);
    }

    [Fact]
    public async Task Load_NonExistent_ReturnsNull()
    {
        var result = await _store.LoadAsync("does-not-exist");
        Assert.Null(result);
    }

    [Fact]
    public async Task Load_CorruptedFile_ReturnsNull()
    {
        // 确保目录存在
        Directory.CreateDirectory(_tempDir);
        var filePath = Path.Combine(_tempDir, "corrupt-session.json");
        await File.WriteAllTextAsync(filePath, "{ this is not valid json }}}");

        var result = await _store.LoadAsync("corrupt-session");
        Assert.Null(result);
    }

    // ===== Exists =====

    [Fact]
    public async Task Exists_ReturnsTrueAfterSave()
    {
        Assert.False(await _store.ExistsAsync("test-session-1"));

        await _store.SaveAsync(CreateTestSnapshot());

        Assert.True(await _store.ExistsAsync("test-session-1"));
    }

    // ===== Delete =====

    [Fact]
    public async Task Delete_RemovesSnapshot()
    {
        await _store.SaveAsync(CreateTestSnapshot());
        Assert.True(await _store.ExistsAsync("test-session-1"));

        await _store.DeleteAsync("test-session-1");
        Assert.False(await _store.ExistsAsync("test-session-1"));
    }

    [Fact]
    public async Task Delete_NonExistent_DoesNotThrow()
    {
        await _store.DeleteAsync("does-not-exist"); // 不应抛异常
    }

    // ===== List =====

    [Fact]
    public async Task List_ReturnsAllSavedSessions()
    {
        var s1 = CreateTestSnapshot("session-a");
        s1.SnapshotAt = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var s2 = CreateTestSnapshot("session-b");
        s2.SnapshotAt = new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc);

        await _store.SaveAsync(s1);
        await _store.SaveAsync(s2);

        var list = await _store.ListAsync();

        Assert.Equal(2, list.Count);
        // 按 SnapshotAt 降序
        Assert.Equal("session-b", list[0].SessionId);
        Assert.Equal("session-a", list[1].SessionId);
        Assert.Equal(1, list[0].AgentCount);
    }

    [Fact]
    public async Task List_EmptyStore_ReturnsEmpty()
    {
        var list = await _store.ListAsync();
        Assert.Empty(list);
    }

    // ===== ContextItemSnapshot 转换 =====

    [Fact]
    public void ContextItemSnapshot_ToContextItem_PreservesFields()
    {
        var snapshot = new ContextItemSnapshot
        {
            Id = "ctx-42",
            Type = ContextItemType.Assistant,
            Seq = 10,
            Content = "test content",
            IsObsolete = true,
            EstimatedTokens = 25
        };

        var item = snapshot.ToContextItem();

        Assert.Equal("ctx-42", item.Id);
        Assert.Equal(ContextItemType.Assistant, item.Type);
        Assert.Equal(10, item.Seq);
        Assert.Equal("test content", item.Content);
        Assert.True(item.IsObsolete);
        Assert.Equal(25, item.EstimatedTokens);
    }

    [Fact]
    public void ContextItemSnapshot_From_CapturesAllFields()
    {
        var item = new ContextItem
        {
            Id = "ctx-99",
            Type = ContextItemType.Window,
            Content = "win-id"
        };
        item.Seq = 7;
        item.IsObsolete = true;
        item.EstimatedTokens = 50;

        var snapshot = ContextItemSnapshot.From(item);

        Assert.Equal("ctx-99", snapshot.Id);
        Assert.Equal(ContextItemType.Window, snapshot.Type);
        Assert.Equal(7, snapshot.Seq);
        Assert.Equal("win-id", snapshot.Content);
        Assert.True(snapshot.IsObsolete);
        Assert.Equal(50, snapshot.EstimatedTokens);
    }

    // ===== JSON 序列化完整性 =====

    [Fact]
    public async Task AppSnapshot_StateData_Survives_RoundTrip()
    {
        var snapshot = CreateTestSnapshot();
        await _store.SaveAsync(snapshot);
        var loaded = await _store.LoadAsync("test-session-1");

        Assert.NotNull(loaded);
        var appState = loaded.Agents[0].Apps[0].StateData;
        Assert.Equal(42, appState["counter"].GetInt32());
        Assert.Equal("test", appState["name"].GetString());
    }

    [Fact]
    public async Task ContextItemType_Enum_SerializesAsString()
    {
        var snapshot = CreateTestSnapshot();
        await _store.SaveAsync(snapshot);

        // 直接读取 JSON 文件验证枚举序列化为字符串
        var filePath = Path.Combine(_tempDir, "test-session-1.json");
        var json = await File.ReadAllTextAsync(filePath);
        Assert.Contains("\"User\"", json);
        Assert.Contains("\"Assistant\"", json);
    }
}
