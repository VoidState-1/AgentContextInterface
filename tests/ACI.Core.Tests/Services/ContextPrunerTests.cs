using ACI.Core.Models;
using ACI.Core.Services;
using ACI.Tests.Common.Fakes;

namespace ACI.Core.Tests.Services;

public class ContextPrunerTests
{
    // 测试点：maxTokens<=0 时应直接返回，不做任何裁剪。
    // 预期结果：输入条目集合保持不变。
    [Fact]
    public void Prune_MaxTokensLessOrEqualZero_ShouldDoNothing()
    {
        var pruner = new ContextPruner();
        var clock = new FakeSeqClock();
        var windowManager = new WindowManager(clock);
        var activeItems = new List<ContextItem>
        {
            CreateItem("c1", ContextItemType.User, content: "1234567890")
        };

        pruner.Prune(activeItems, windowManager, maxTokens: 0, minConversationTokens: 0, pruneTargetTokens: 0);

        Assert.Single(activeItems);
    }

    // 测试点：总 token 未超过上限时，不应触发裁剪。
    // 预期结果：条目数量与顺序不变。
    [Fact]
    public void Prune_TotalWithinLimit_ShouldDoNothing()
    {
        var pruner = new ContextPruner();
        var clock = new FakeSeqClock();
        var windowManager = new WindowManager(clock);
        var activeItems = new List<ContextItem>
        {
            CreateItem("c1", ContextItemType.User, content: "1234"),
            CreateItem("c2", ContextItemType.Assistant, content: "5678")
        };

        pruner.Prune(activeItems, windowManager, maxTokens: 10, minConversationTokens: 0, pruneTargetTokens: 0);

        Assert.Equal(["c1", "c2"], activeItems.Select(i => i.Id));
    }

    // 测试点：pruneTargetTokens<=0 时应默认收缩到 maxTokens 的一半。
    // 预期结果：在超限场景下条目被裁剪到目标以下。
    [Fact]
    public void Prune_DefaultTarget_ShouldFallbackToHalfOfMaxTokens()
    {
        var pruner = new ContextPruner();
        var clock = new FakeSeqClock();
        var windowManager = new WindowManager(clock);
        var activeItems = new List<ContextItem>
        {
            CreateItem("c1", ContextItemType.User, content: "1234567890"),
            CreateItem("c2", ContextItemType.Assistant, content: "1234567890"),
            CreateItem("c3", ContextItemType.System, content: "1234567890")
        };

        pruner.Prune(activeItems, windowManager, maxTokens: 10, minConversationTokens: 0, pruneTargetTokens: 0);

        Assert.Single(activeItems);
        Assert.Equal("c3", activeItems[0].Id);
    }

    // 测试点：第一轮应优先清理对话，再清理非重要窗口，且遵守对话保护阈值。
    // 预期结果：保留一条对话（满足保护阈值），并移除非重要窗口。
    [Fact]
    public void Prune_FirstRound_ShouldRespectConversationProtectionAndRemoveNonImportantWindow()
    {
        var pruner = new ContextPruner();
        var clock = new FakeSeqClock();
        var windowManager = new WindowManager(clock);
        windowManager.Add(new Window
        {
            Id = "w-non-important",
            Content = new TextContent(new string('w', 120)),
            Options = new WindowOptions
            {
                Important = false
            }
        });

        var activeItems = new List<ContextItem>
        {
            CreateItem("u1", ContextItemType.User, content: "1234567890"),
            CreateItem("a1", ContextItemType.Assistant, content: "1234567890"),
            CreateItem("w1", ContextItemType.Window, content: "w-non-important")
        };

        pruner.Prune(activeItems, windowManager, maxTokens: 9, minConversationTokens: 4, pruneTargetTokens: 4);

        Assert.Contains(activeItems, i => i.Id == "a1");
        Assert.DoesNotContain(activeItems, i => i.Id == "u1");
        Assert.DoesNotContain(activeItems, i => i.Id == "w1");
    }

    // 测试点：第二轮在仍超限时应继续裁剪重要窗口（非 Pin）。
    // 预期结果：重要窗口被移除，对话条目保留。
    [Fact]
    public void Prune_SecondRound_ShouldRemoveImportantWindowWhenStillOverTarget()
    {
        var pruner = new ContextPruner();
        var clock = new FakeSeqClock();
        var windowManager = new WindowManager(clock);
        windowManager.Add(new Window
        {
            Id = "w-important",
            Content = new TextContent(new string('w', 120)),
            Options = new WindowOptions
            {
                Important = true,
                PinInPrompt = false
            }
        });

        var activeItems = new List<ContextItem>
        {
            CreateItem("a1", ContextItemType.Assistant, content: "1234567890"),
            CreateItem("w1", ContextItemType.Window, content: "w-important")
        };

        pruner.Prune(activeItems, windowManager, maxTokens: 9, minConversationTokens: 4, pruneTargetTokens: 4);

        Assert.Contains(activeItems, i => i.Id == "a1");
        Assert.DoesNotContain(activeItems, i => i.Id == "w1");
    }

    // 测试点：PinInPrompt=true 的窗口不应在任一轮被裁剪。
    // 预期结果：即使超限，固定窗口仍保留。
    [Fact]
    public void Prune_PinnedWindow_ShouldNeverBeRemoved()
    {
        var pruner = new ContextPruner();
        var clock = new FakeSeqClock();
        var windowManager = new WindowManager(clock);
        windowManager.Add(new Window
        {
            Id = "w-pinned",
            Content = new TextContent(new string('w', 120)),
            Options = new WindowOptions
            {
                Important = true,
                PinInPrompt = true
            }
        });

        var activeItems = new List<ContextItem>
        {
            CreateItem("a1", ContextItemType.Assistant, content: "1234567890"),
            CreateItem("w1", ContextItemType.Window, content: "w-pinned")
        };

        pruner.Prune(activeItems, windowManager, maxTokens: 9, minConversationTokens: 4, pruneTargetTokens: 4);

        Assert.Contains(activeItems, i => i.Id == "a1");
        Assert.Contains(activeItems, i => i.Id == "w1");
    }

    // 测试点：IsObsolete=true 的条目不应参与候选集计算与删除。
    // 预期结果：过时条目保持原样，不影响本轮裁剪行为。
    [Fact]
    public void Prune_ObsoleteItems_ShouldBeIgnored()
    {
        var pruner = new ContextPruner();
        var clock = new FakeSeqClock();
        var windowManager = new WindowManager(clock);
        var activeItems = new List<ContextItem>
        {
            CreateItem("old", ContextItemType.User, content: new string('x', 200), isObsolete: true),
            CreateItem("new", ContextItemType.Assistant, content: "1234")
        };

        pruner.Prune(activeItems, windowManager, maxTokens: 3, minConversationTokens: 0, pruneTargetTokens: 2);

        Assert.Equal(2, activeItems.Count);
        Assert.True(activeItems.Single(i => i.Id == "old").IsObsolete);
    }

    private static ContextItem CreateItem(
        string id,
        ContextItemType type,
        string content,
        bool isObsolete = false)
    {
        return new ContextItem
        {
            Id = id,
            Type = type,
            Content = content,
            IsObsolete = isObsolete
        };
    }
}
