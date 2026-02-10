using ACI.Framework.Runtime;

namespace ACI.Framework.Tests.Runtime;

public class InMemoryAppStateTests
{
    // 测试点：Set/Get/Has 应支持基础状态读写。
    // 预期结果：写入后可读取，Has 返回 true。
    [Fact]
    public void SetAndGet_ShouldPersistValue()
    {
        var state = new InMemoryAppState();

        state.Set("count", 3);

        Assert.True(state.Has("count"));
        Assert.Equal(3, state.Get<int>("count"));
    }

    // 测试点：Clear 应清空所有状态键。
    // 预期结果：清空后 Has 返回 false，Get 返回默认值。
    [Fact]
    public void Clear_ShouldRemoveAllValues()
    {
        var state = new InMemoryAppState();
        state.Set("name", "aci");
        state.Set("count", 7);

        state.Clear();

        Assert.False(state.Has("name"));
        Assert.False(state.Has("count"));
        Assert.Equal(default, state.Get<int>("count"));
    }
}
