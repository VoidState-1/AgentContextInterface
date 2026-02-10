using ACI.Core.Abstractions;
using ACI.Core.Models;
using ACI.Core.Services;
using ACI.Tests.Common.Fakes;

namespace ACI.Core.Tests.Services;

public class WindowManagerTests
{
    // 测试点：Add 应设置窗口元数据并发布 Created 事件。
    // 预期结果：CreatedAt/UpdatedAt 为当前序号，事件类型为 Created。
    [Fact]
    public void Add_ShouldSetMetaAndPublishCreatedEvent()
    {
        var clock = new FakeSeqClock();
        var manager = new WindowManager(clock);
        var changedEvents = new List<WindowChangedEvent>();
        manager.OnChanged += evt => changedEvents.Add(evt);

        var window = CreateWindow("w-1");
        manager.Add(window);

        Assert.Equal(0, window.Meta.CreatedAt);
        Assert.Equal(0, window.Meta.UpdatedAt);
        var evt = Assert.Single(changedEvents);
        Assert.Equal(WindowEventType.Created, evt.Type);
        Assert.Equal("w-1", evt.WindowId);
    }

    // 测试点：Remove 已存在窗口时应移除并发布 Removed 事件。
    // 预期结果：Get 返回 null，事件类型为 Removed。
    [Fact]
    public void Remove_ExistingWindow_ShouldRemoveAndPublishEvent()
    {
        var clock = new FakeSeqClock();
        var manager = new WindowManager(clock);
        var changedEvents = new List<WindowChangedEvent>();
        manager.OnChanged += evt => changedEvents.Add(evt);

        manager.Add(CreateWindow("w-2"));
        changedEvents.Clear();
        manager.Remove("w-2");

        Assert.Null(manager.Get("w-2"));
        var evt = Assert.Single(changedEvents);
        Assert.Equal(WindowEventType.Removed, evt.Type);
        Assert.Equal("w-2", evt.WindowId);
    }

    // 测试点：Remove 不存在窗口时不应触发事件。
    // 预期结果：事件列表保持为空。
    [Fact]
    public void Remove_MissingWindow_ShouldNotPublishEvent()
    {
        var clock = new FakeSeqClock();
        var manager = new WindowManager(clock);
        var changedEvents = new List<WindowChangedEvent>();
        manager.OnChanged += evt => changedEvents.Add(evt);

        manager.Remove("missing");

        Assert.Empty(changedEvents);
    }

    // 测试点：NotifyUpdated 应更新 UpdatedAt 并发布 Updated 事件。
    // 预期结果：UpdatedAt 更新到当前时钟序号，事件类型为 Updated。
    [Fact]
    public void NotifyUpdated_ShouldUpdateMetaAndPublishEvent()
    {
        var clock = new FakeSeqClock();
        var manager = new WindowManager(clock);
        var changedEvents = new List<WindowChangedEvent>();
        manager.OnChanged += evt => changedEvents.Add(evt);

        var window = CreateWindow("w-3");
        manager.Add(window);
        changedEvents.Clear();
        clock.Next();

        manager.NotifyUpdated("w-3");

        Assert.Equal(1, window.Meta.UpdatedAt);
        var evt = Assert.Single(changedEvents);
        Assert.Equal(WindowEventType.Updated, evt.Type);
        Assert.Equal("w-3", evt.WindowId);
    }

    // 测试点：GetAllOrdered 应按 CreatedAt 升序返回窗口。
    // 预期结果：先创建的窗口排在前面。
    [Fact]
    public void GetAllOrdered_ShouldReturnByCreatedAt()
    {
        var clock = new FakeSeqClock();
        var manager = new WindowManager(clock);

        manager.Add(CreateWindow("w-old"));
        clock.Next();
        manager.Add(CreateWindow("w-new"));

        var ordered = manager.GetAllOrdered().Select(w => w.Id).ToList();

        Assert.Equal(["w-old", "w-new"], ordered);
    }

    private static Window CreateWindow(string id)
    {
        return new Window
        {
            Id = id,
            Content = new TextContent("content")
        };
    }
}

