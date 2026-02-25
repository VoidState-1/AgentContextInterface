using ACI.Core.Models;
using ACI.Core.Services;
using ACI.Tests.Common.Fakes;

namespace ACI.Core.Tests.Services;

public class ContextRendererTests
{
    // 测试点：System/User/Assistant 条目应映射到对应 LLM role。
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

    // 测试点：Window 条目引用不存在窗口时应被跳过。
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

    // 测试点：namespace 应在首次被窗口引用时追加到当前上下文末尾，而不是前置。
    [Fact]
    public void Render_NamespaceDefinitions_ShouldAppendWhenFirstReferenced()
    {
        var renderer = new ContextRenderer();
        var clock = new FakeSeqClock();
        var windows = new WindowManager(clock);

        windows.Add(new Window
        {
            Id = "w-1",
            Content = new TextContent("file window"),
            NamespaceRefs = ["file_explorer"]
        });

        windows.Add(new Window
        {
            Id = "w-2",
            Content = new TextContent("system window"),
            NamespaceRefs = ["file_explorer", "system"]
        });

        var registry = new ActionNamespaceRegistry();
        registry.Upsert(CreateNamespace("file_explorer", "open"));
        registry.Upsert(CreateNamespace("system", "close"));

        var items = new List<ContextItem>
        {
            new()
            {
                Id = "u1",
                Type = ContextItemType.User,
                Content = "before"
            },
            new()
            {
                Id = "w1",
                Type = ContextItemType.Window,
                Content = "w-1"
            },
            new()
            {
                Id = "a1",
                Type = ContextItemType.Assistant,
                Content = "between"
            },
            new()
            {
                Id = "w2",
                Type = ContextItemType.Window,
                Content = "w-2"
            }
        };

        var messages = renderer.Render(items, windows, registry);

        Assert.Equal("before", messages[0].Content);
        Assert.Contains("<Window", messages[1].Content);
        Assert.Contains("<namespace id=\"file_explorer\"", messages[2].Content);
        Assert.Equal("between", messages[3].Content);
        Assert.Contains("<Window", messages[4].Content);
        Assert.Contains("<namespace id=\"system\"", messages[5].Content);
    }

    // 测试点：同一 namespace 即使被多个窗口引用，也只注入一次。
    [Fact]
    public void Render_NamespaceDefinitions_ShouldNotDuplicateAcrossWindows()
    {
        var renderer = new ContextRenderer();
        var clock = new FakeSeqClock();
        var windows = new WindowManager(clock);

        windows.Add(new Window
        {
            Id = "w-1",
            Content = new TextContent("first"),
            NamespaceRefs = ["file_explorer"]
        });

        windows.Add(new Window
        {
            Id = "w-2",
            Content = new TextContent("second"),
            NamespaceRefs = ["file_explorer"]
        });

        var registry = new ActionNamespaceRegistry();
        registry.Upsert(CreateNamespace("file_explorer", "open"));

        var items = new List<ContextItem>
        {
            new()
            {
                Id = "w1",
                Type = ContextItemType.Window,
                Content = "w-1"
            },
            new()
            {
                Id = "w2",
                Type = ContextItemType.Window,
                Content = "w-2"
            }
        };

        var messages = renderer.Render(items, windows, registry);

        Assert.Equal(3, messages.Count);
        Assert.Contains("<Window", messages[0].Content);
        Assert.Contains("<namespace id=\"file_explorer\"", messages[1].Content);
        Assert.Contains("<Window", messages[2].Content);
    }

    // 构造最小命名空间定义，供渲染测试使用。
    private static ActionNamespaceDefinition CreateNamespace(string namespaceId, string actionId)
    {
        return new ActionNamespaceDefinition
        {
            Id = namespaceId,
            Actions =
            [
                new ActionDescriptor
                {
                    Id = actionId,
                    Description = "test action",
                    Params = new Dictionary<string, string>
                    {
                        ["path"] = "string?"
                    }
                }
            ]
        };
    }
}
