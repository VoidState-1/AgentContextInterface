using ACI.Core.Services;

namespace ACI.Core.Tests.Services;

public class SeqClockTests
{
    // 测试点：SeqClock.Next 应按 1 递增，CurrentSeq 同步更新。
    // 预期结果：连续调用返回 1、2，CurrentSeq 最终为 2。
    [Fact]
    public void Next_ShouldIncrementSequence()
    {
        var clock = new SeqClock();

        var first = clock.Next();
        var second = clock.Next();

        Assert.Equal(1, first);
        Assert.Equal(2, second);
        Assert.Equal(2, clock.CurrentSeq);
    }
}

