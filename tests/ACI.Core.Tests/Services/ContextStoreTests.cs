using ACI.Core.Models;
using ACI.Core.Services;
using ACI.Tests.Common.Fakes;

namespace ACI.Core.Tests.Services;

public class ContextStoreTests
{
    // 测试点：Add 应分配递增 Seq，并为非窗口条目估算 token。
    // 预期结果：Seq 从 1 开始，EstimatedTokens 大于 0。
    [Fact]
    public void Add_TextItem_ShouldAssignSeqAndEstimateTokens()
    {
        var clock = new FakeSeqClock();
        var store = new ContextStore(clock);

        store.Add(new ContextItem
        {
            Id = "c-1",
            Type = ContextItemType.User,
            Content = "abcdef"
        });

        var item = Assert.Single(store.GetAll());
        Assert.Equal(1, item.Seq);
        Assert.True(item.EstimatedTokens > 0);
    }

    // 测试点：同一窗口新增条目时，旧窗口条目应被标记为过时。
    // 预期结果：旧条目 IsObsolete=true，新条目 IsObsolete=false。
    [Fact]
    public void Add_NewWindowVersion_ShouldMarkOldVersionObsolete()
    {
        var clock = new FakeSeqClock();
        var store = new ContextStore(clock);

        store.Add(new ContextItem
        {
            Id = "w-item-1",
            Type = ContextItemType.Window,
            Content = "window-a"
        });
        store.Add(new ContextItem
        {
            Id = "w-item-2",
            Type = ContextItemType.Window,
            Content = "window-a"
        });

        var all = store.GetAll();
        Assert.Equal(2, all.Count);
        Assert.True(all[0].IsObsolete);
        Assert.False(all[1].IsObsolete);
    }

    // 测试点：GetAll 应按 Seq 升序返回。
    // 预期结果：返回顺序与添加顺序一致。
    [Fact]
    public void GetAll_ShouldReturnItemsOrderedBySeq()
    {
        var clock = new FakeSeqClock();
        var store = new ContextStore(clock);

        store.Add(new ContextItem
        {
            Id = "a",
            Type = ContextItemType.User,
            Content = "first"
        });
        store.Add(new ContextItem
        {
            Id = "b",
            Type = ContextItemType.Assistant,
            Content = "second"
        });

        var all = store.GetAll();
        Assert.Equal("a", all[0].Id);
        Assert.Equal("b", all[1].Id);
    }

    // 测试点：GetActive 需过滤过时条目。
    // 预期结果：仅返回 IsObsolete=false 的条目。
    [Fact]
    public void GetActive_ShouldExcludeObsoleteItems()
    {
        var clock = new FakeSeqClock();
        var store = new ContextStore(clock);

        store.Add(new ContextItem
        {
            Id = "w-item-1",
            Type = ContextItemType.Window,
            Content = "window-a"
        });
        store.Add(new ContextItem
        {
            Id = "w-item-2",
            Type = ContextItemType.Window,
            Content = "window-a"
        });

        var active = store.GetActive();
        var item = Assert.Single(active);
        Assert.Equal("w-item-2", item.Id);
    }

    // 测试点：GetById 应可从归档索引检索条目。
    // 预期结果：按 ID 命中正确对象，找不到时返回 null。
    [Fact]
    public void GetById_ShouldReturnItemFromArchiveIndex()
    {
        var clock = new FakeSeqClock();
        var store = new ContextStore(clock);
        store.Add(new ContextItem
        {
            Id = "id-1",
            Type = ContextItemType.System,
            Content = "sys"
        });

        var found = store.GetById("id-1");
        var missing = store.GetById("missing");

        Assert.NotNull(found);
        Assert.Equal("id-1", found!.Id);
        Assert.Null(missing);
    }

    // 测试点：MarkWindowObsolete 应将指定窗口的所有条目标记为过时。
    // 预期结果：匹配窗口条目均为 IsObsolete=true。
    [Fact]
    public void MarkWindowObsolete_ShouldMarkAllWindowItems()
    {
        var clock = new FakeSeqClock();
        var store = new ContextStore(clock);
        store.Add(new ContextItem
        {
            Id = "w1-v1",
            Type = ContextItemType.Window,
            Content = "w1"
        });
        store.Add(new ContextItem
        {
            Id = "w1-v2",
            Type = ContextItemType.Window,
            Content = "w1"
        });
        store.Add(new ContextItem
        {
            Id = "w2-v1",
            Type = ContextItemType.Window,
            Content = "w2"
        });

        store.MarkWindowObsolete("w1");

        var all = store.GetAll();
        Assert.True(all.Where(i => i.Content == "w1").All(i => i.IsObsolete));
        Assert.False(all.Single(i => i.Content == "w2").IsObsolete);
    }

    // 测试点：GetWindowItem 应返回指定窗口最新且未过时的条目。
    // 预期结果：返回新版本条目 ID。
    [Fact]
    public void GetWindowItem_ShouldReturnLatestActiveWindowItem()
    {
        var clock = new FakeSeqClock();
        var store = new ContextStore(clock);
        store.Add(new ContextItem
        {
            Id = "v1",
            Type = ContextItemType.Window,
            Content = "w1"
        });
        store.Add(new ContextItem
        {
            Id = "v2",
            Type = ContextItemType.Window,
            Content = "w1"
        });

        var latest = store.GetWindowItem("w1");

        Assert.NotNull(latest);
        Assert.Equal("v2", latest!.Id);
    }

    // 测试点：执行 Prune 时应只删除活跃集条目，归档集应完整保留。
    // 预期结果：GetActive 数量下降，GetArchive 数量不变。
    [Fact]
    public void Prune_ShouldRemoveActiveItemsButKeepArchive()
    {
        var clock = new FakeSeqClock();
        var store = new ContextStore(clock);
        var windowManager = new WindowManager(clock);
        windowManager.Add(new Window
        {
            Id = "w-prune",
            Content = new TextContent(new string('x', 120)),
            Options = new WindowOptions
            {
                Important = false
            }
        });

        store.Add(new ContextItem
        {
            Id = "u-1",
            Type = ContextItemType.User,
            Content = new string('u', 200)
        });
        store.Add(new ContextItem
        {
            Id = "w-1",
            Type = ContextItemType.Window,
            Content = "w-prune"
        });

        var beforeActiveCount = store.GetActive().Count;
        var beforeArchiveCount = store.GetArchive().Count;

        store.Prune(
            new ContextPruner(),
            windowManager,
            maxTokens: 10,
            minConversationTokens: 0,
            pruneTargetTokens: 5);

        var afterActiveCount = store.GetActive().Count;
        var afterArchiveCount = store.GetArchive().Count;

        Assert.True(afterActiveCount < beforeActiveCount);
        Assert.Equal(beforeArchiveCount, afterArchiveCount);
    }

    // 测试点：恢复快照时，已被裁剪条目不应重新进入 active 视图。
    // 预期结果：恢复后的 active 与保存时 active 一致；archive 保持完整历史。
    [Fact]
    public void ImportSnapshotItems_ShouldNotResurrectPrunedItemsIntoActive()
    {
        var clock = new FakeSeqClock();
        var store = new ContextStore(clock);
        var windowManager = new WindowManager(clock);

        store.Add(new ContextItem
        {
            Id = "u-1",
            Type = ContextItemType.User,
            Content = new string('a', 200)
        });
        store.Add(new ContextItem
        {
            Id = "u-2",
            Type = ContextItemType.Assistant,
            Content = new string('b', 200)
        });

        store.Prune(
            new ContextPruner(),
            windowManager,
            maxTokens: 10,
            minConversationTokens: 0,
            pruneTargetTokens: 5);

        var savedActive = store.ExportActiveItems();
        var savedArchive = store.ExportArchiveItems();
        Assert.True(savedArchive.Count > savedActive.Count);

        var restored = new ContextStore(new FakeSeqClock());
        restored.ImportSnapshotItems(savedActive, savedArchive);

        var restoredActiveIds = restored.GetActive().Select(i => i.Id).ToList();
        var savedActiveIds = savedActive.Where(i => !i.IsObsolete).Select(i => i.Id).ToList();
        Assert.Equal(savedActiveIds, restoredActiveIds);
        Assert.Equal(savedArchive.Count, restored.GetArchive().Count);
    }
}
