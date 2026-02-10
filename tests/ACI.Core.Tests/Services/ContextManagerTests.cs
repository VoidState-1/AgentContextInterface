using ACI.Core.Models;
using ACI.Core.Services;
using ACI.Tests.Common.Fakes;

namespace ACI.Core.Tests.Services;

public class ContextManagerTests
{
    // 测试点：ContextManager.Add 后应可通过 GetActive/GetArchive 读取条目。
    // 预期结果：活跃集与归档集均包含新增条目。
    [Fact]
    public void Add_ShouldAppearInActiveAndArchive()
    {
        var manager = new ContextManager(new FakeSeqClock());
        manager.Add(new ContextItem
        {
            Id = "c-1",
            Type = ContextItemType.User,
            Content = "hello"
        });

        Assert.Single(manager.GetActive());
        Assert.Single(manager.GetArchive());
    }

    // 测试点：Prune 应通过统一入口触发裁剪逻辑。
    // 预期结果：超限时活跃条目数量下降。
    [Fact]
    public void Prune_ShouldReduceActiveItemsWhenOverBudget()
    {
        var clock = new FakeSeqClock();
        var manager = new ContextManager(clock);
        var windows = new WindowManager(clock);
        windows.Add(new Window
        {
            Id = "w-1",
            Content = new TextContent(new string('x', 120)),
            Options = new WindowOptions
            {
                Important = false
            }
        });

        manager.Add(new ContextItem
        {
            Id = "u-1",
            Type = ContextItemType.User,
            Content = new string('u', 200)
        });
        manager.Add(new ContextItem
        {
            Id = "w-item",
            Type = ContextItemType.Window,
            Content = "w-1"
        });

        var before = manager.GetActive().Count;
        manager.Prune(windows, maxTokens: 10, minConversationTokens: 0, pruneTargetTokens: 5);
        var after = manager.GetActive().Count;

        Assert.True(after < before);
    }
}

