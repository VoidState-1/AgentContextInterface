using ACI.Core.Abstractions;
using ACI.Core.Models;
using ACI.Tests.Common.TestData;

namespace ACI.Core.Tests.Abstractions;

public class ActionContextTests
{
    // 测试点：GetValue 在参数缺失时应返回 null。
    // 预期结果：未命中字段返回 null。
    [Fact]
    public void GetValue_MissingProperty_ShouldReturnNull()
    {
        var context = CreateContext("""{"name":"aci"}""");

        var value = context.GetValue("missing");

        Assert.Null(value);
    }

    // 测试点：GetString 应支持 string/number/bool 的字符串化读取。
    // 预期结果：三种类型均能按规则返回字符串。
    [Fact]
    public void GetString_MultipleKinds_ShouldConvertCorrectly()
    {
        var context = CreateContext("""{"text":"ok","num":12,"flag":true}""");

        Assert.Equal("ok", context.GetString("text"));
        Assert.Equal("12", context.GetString("num"));
        Assert.Equal("true", context.GetString("flag"));
    }

    // 测试点：GetInt 应支持 number 与可解析字符串。
    // 预期结果：可解析值返回整数，非法值返回 null。
    [Fact]
    public void GetInt_NumberOrParsableString_ShouldReturnInt()
    {
        var context = CreateContext("""{"n1":7,"n2":"8","bad":"x"}""");

        Assert.Equal(7, context.GetInt("n1"));
        Assert.Equal(8, context.GetInt("n2"));
        Assert.Null(context.GetInt("bad"));
    }

    // 测试点：GetBool 应支持 bool/字符串布尔并处理默认值。
    // 预期结果：可解析值按值返回，缺失字段返回默认值，非法字符串返回 false。
    [Fact]
    public void GetBool_ShouldRespectParsingAndDefaultValue()
    {
        var context = CreateContext("""{"b1":true,"b2":"false","bad":"x"}""");

        Assert.True(context.GetBool("b1"));
        Assert.False(context.GetBool("b2", defaultValue: true));
        Assert.False(context.GetBool("bad", defaultValue: true));
        Assert.False(context.GetBool("missing", defaultValue: false));
    }

    // 测试点：GetAs<T> 应在 JSON 匹配时反序列化成功，异常时返回 default。
    // 预期结果：匹配字段返回对象，不匹配字段返回 null。
    [Fact]
    public void GetAs_ShouldDeserializeOrReturnDefault()
    {
        var context = CreateContext("""{"cfg":{"Name":"aci","Size":3},"invalid":"not-object"}""");

        var config = context.GetAs<TestConfig>("cfg");
        var invalid = context.GetAs<TestConfig>("invalid");

        Assert.NotNull(config);
        Assert.Equal("aci", config!.Name);
        Assert.Equal(3, config.Size);
        Assert.Null(invalid);
    }

    private static ActionContext CreateContext(string json)
    {
        return new ActionContext
        {
            Window = new Window
            {
                Id = "w1",
                Content = new TextContent("content")
            },
            ActionId = "test",
            Parameters = TestJson.Parse(json)
        };
    }

    public sealed class TestConfig
    {
        public string? Name { get; set; }

        public int Size { get; set; }
    }
}
