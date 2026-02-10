using ACI.Core.Models;
using ACI.Core.Services;
using ACI.Tests.Common.Fakes;

namespace ACI.Core.Tests.Services;

public class ContextRendererTests
{
    // 测试点：System/User/Assistant 条目应映射到对应 LLM role。
    // 预期结果：输出消息角色与输入类型一一对应。
    [Fact]
    public void Render_BasicContextTypes_ShouldMapRolesCorrectly()
    {
        var renderer = new ContextRenderer();
        var clock = new FakeSeqClock();
        var windows = new WindowManager(clock);
        var items = new List<ContextItem>
        {
            new()
            {
                Id = "s1",
                Type = ContextItemType.System,
                Content = "sys"
            },
            new()
            {
                Id = "u1",
                Type = ContextItemType.User,
                Content = "user"
            },
            new()
            {
                Id = "a1",
                Type = ContextItemType.Assistant,
                Content = "assistant"
            }
        };

        var messages = renderer.Render(items, windows);

        Assert.Equal(["system", "user", "assistant"], messages.Select(m => m.Role));
        Assert.Equal(["sys", "user", "assistant"], messages.Select(m => m.Content));
    }

    // 测试点：Window 类型条目应从 WindowManager 拉取实时渲染内容。
    // 预期结果：输出 role=user，content 为窗口 XML。
    [Fact]
    public void Render_WindowItem_ShouldRenderWindowAsUserMessage()
    {
        var renderer = new ContextRenderer();
        var clock = new FakeSeqClock();
        var windows = new WindowManager(clock);
        windows.Add(new Window
        {
            Id = "w-1",
            Content = new TextContent("window-content")
        });

        var items = new List<ContextItem>
        {
            new()
            {
                Id = "w-item",
                Type = ContextItemType.Window,
                Content = "w-1"
            }
        };

        var messages = renderer.Render(items, windows);

        var message = Assert.Single(messages);
        Assert.Equal("user", message.Role);
        Assert.Contains("<Window", message.Content);
        Assert.Contains("id=\"w-1\"", message.Content);
    }

    // 测试点：Window 条目引用已不存在窗口时应被跳过。
    // 预期结果：输出消息列表中不包含该条目。
    [Fact]
    public void Render_MissingWindow_ShouldSkipItem()
    {
        var renderer = new ContextRenderer();
        var clock = new FakeSeqClock();
        var windows = new WindowManager(clock);
        var items = new List<ContextItem>
        {
            new()
            {
                Id = "w-item",
                Type = ContextItemType.Window,
                Content = "missing-window"
            }
        };

        var messages = renderer.Render(items, windows);

        Assert.Empty(messages);
    }

    // 测试点：渲染结果应保持输入上下文顺序。
    // 预期结果：输出消息顺序与输入条目顺序一致。
    [Fact]
    public void Render_MixedItems_ShouldKeepInputOrder()
    {
        var renderer = new ContextRenderer();
        var clock = new FakeSeqClock();
        var windows = new WindowManager(clock);
        windows.Add(new Window
        {
            Id = "w-2",
            Content = new TextContent("window-content")
        });

        var items = new List<ContextItem>
        {
            new()
            {
                Id = "s1",
                Type = ContextItemType.System,
                Content = "sys"
            },
            new()
            {
                Id = "w1",
                Type = ContextItemType.Window,
                Content = "w-2"
            },
            new()
            {
                Id = "u1",
                Type = ContextItemType.User,
                Content = "user"
            }
        };

        var messages = renderer.Render(items, windows);

        Assert.Equal(3, messages.Count);
        Assert.Equal("system", messages[0].Role);
        Assert.Equal("user", messages[1].Role);
        Assert.Equal("user", messages[2].Role);
        Assert.Equal("sys", messages[0].Content);
        Assert.Equal("user", messages[2].Content);
    }
}
