using System.Text.Json;
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

    // ========== Export/Import 测试 ==========

    // 测试点：Export 应将所有状态导出为 JsonElement 字典。
    [Fact]
    public void Export_ShouldSerializeAllEntries()
    {
        var state = new InMemoryAppState();
        state.Set("name", "aci");
        state.Set("count", 42);
        state.Set("active", true);

        var exported = state.Export();

        Assert.Equal(3, exported.Count);
        Assert.True(exported.ContainsKey("name"));
        Assert.True(exported.ContainsKey("count"));
        Assert.True(exported.ContainsKey("active"));
    }

    // 测试点：Import 后 Get 应返回正确的值。
    [Fact]
    public void Import_ShouldRestoreSimpleValues()
    {
        // 1. 创建原始状态并导出
        var original = new InMemoryAppState();
        original.Set("name", "aci");
        original.Set("count", 42);
        original.Set("ratio", 3.14);
        var exported = original.Export();

        // 2. 创建新状态并导入
        var restored = new InMemoryAppState();
        restored.Import(exported);

        // 3. 验证恢复结果
        Assert.Equal("aci", restored.Get<string>("name"));
        Assert.Equal(42, restored.Get<int>("count"));
        Assert.Equal(3.14, restored.Get<double>("ratio"));
        Assert.True(restored.Has("name"));
    }

    // 测试点：Import 应支持复杂类型（列表）。
    [Fact]
    public void Import_ShouldRestoreCollections()
    {
        var original = new InMemoryAppState();
        var items = new List<string> { "alpha", "beta", "gamma" };
        original.Set("items", items);
        var exported = original.Export();

        var restored = new InMemoryAppState();
        restored.Import(exported);

        var result = restored.Get<List<string>>("items");
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal("alpha", result[0]);
        Assert.Equal("gamma", result[2]);
    }

    // 测试点：Import 应支持嵌套对象。
    [Fact]
    public void Import_ShouldRestoreNestedObjects()
    {
        var original = new InMemoryAppState();
        var data = new TestData { Label = "hello", Value = 99 };
        original.Set("data", data);
        var exported = original.Export();

        var restored = new InMemoryAppState();
        restored.Import(exported);

        var result = restored.Get<TestData>("data");
        Assert.NotNull(result);
        Assert.Equal("hello", result.Label);
        Assert.Equal(99, result.Value);
    }

    // 测试点：Import 应覆盖现有数据。
    [Fact]
    public void Import_ShouldOverwriteExistingData()
    {
        var state = new InMemoryAppState();
        state.Set("old_key", "old_value");

        var newData = new Dictionary<string, JsonElement>
        {
            ["new_key"] = JsonDocument.Parse("\"new_value\"").RootElement.Clone()
        };
        state.Import(newData);

        Assert.False(state.Has("old_key"));
        Assert.True(state.Has("new_key"));
        Assert.Equal("new_value", state.Get<string>("new_key"));
    }

    // 测试点：Export 已导入数据应产生相同结果（往返一致性）。
    [Fact]
    public void ExportImport_RoundTrip_ShouldBeConsistent()
    {
        var original = new InMemoryAppState();
        original.Set("greeting", "hello");
        original.Set("numbers", new List<int> { 1, 2, 3 });

        // 第一次导出
        var exported1 = original.Export();

        // 导入到新状态
        var restored = new InMemoryAppState();
        restored.Import(exported1);

        // 第二次导出
        var exported2 = restored.Export();

        // 验证两次导出键集合相同
        Assert.Equal(exported1.Keys.OrderBy(k => k), exported2.Keys.OrderBy(k => k));

        // 验证值一致
        Assert.Equal("hello", restored.Get<string>("greeting"));
        var numbers = restored.Get<List<int>>("numbers");
        Assert.NotNull(numbers);
        Assert.Equal(new[] { 1, 2, 3 }, numbers);
    }

    // 测试点：Get<T> 在 Import 后应缓存反序列化结果。
    [Fact]
    public void Get_AfterImport_ShouldCacheDeserializedResult()
    {
        var original = new InMemoryAppState();
        original.Set("data", new TestData { Label = "cached", Value = 1 });
        var exported = original.Export();

        var restored = new InMemoryAppState();
        restored.Import(exported);

        // 第一次 Get 触发反序列化
        var first = restored.Get<TestData>("data");
        // 第二次 Get 应返回缓存的同一实例
        var second = restored.Get<TestData>("data");

        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    // 测试点：Export 空状态应返回空字典。
    [Fact]
    public void Export_Empty_ShouldReturnEmptyDictionary()
    {
        var state = new InMemoryAppState();
        var exported = state.Export();
        Assert.Empty(exported);
    }

    // 测试点：Import 空字典应清空现有数据。
    [Fact]
    public void Import_EmptyDictionary_ShouldClearState()
    {
        var state = new InMemoryAppState();
        state.Set("key", "value");

        state.Import(new Dictionary<string, JsonElement>());

        Assert.False(state.Has("key"));
    }

    private class TestData
    {
        public string Label { get; set; } = "";
        public int Value { get; set; }
    }
}
