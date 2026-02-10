using ACI.Core.Models;
using ACI.Framework.Runtime;
using System.Text.Json;

namespace ACI.Framework.Tests.Runtime;

public class ParamTests
{
    // 测试点：字符串参数工厂应构建正确的基础字段。
    // 预期结果：Kind/Required/Description 都与传入值一致。
    [Fact]
    public void String_ShouldBuildExpectedSchema()
    {
        var schema = Param.String(required: false, description: "查询关键字");

        Assert.Equal(ActionParamKind.String, schema.Kind);
        Assert.False(schema.Required);
        Assert.Equal("查询关键字", schema.Description);
    }

    // 测试点：数字参数默认值应被序列化为 JsonElement。
    // 预期结果：Default 存在且值类型为 Number。
    [Fact]
    public void Number_WithDefault_ShouldSerializeToJsonElement()
    {
        var schema = Param.Number(defaultValue: 3.14);

        Assert.NotNull(schema.Default);
        Assert.Equal(JsonValueKind.Number, schema.Default!.Value.ValueKind);
        Assert.Equal(3.14, schema.Default.Value.GetDouble(), 3);
    }

    // 测试点：布尔参数默认值应保持布尔类型。
    // 预期结果：Default 的 ValueKind 为 True 或 False。
    [Fact]
    public void Boolean_WithDefault_ShouldKeepBooleanKind()
    {
        var schema = Param.Boolean(defaultValue: true);

        Assert.NotNull(schema.Default);
        Assert.Equal(JsonValueKind.True, schema.Default!.Value.ValueKind);
    }

    // 测试点：数组参数工厂应携带元素类型定义。
    // 预期结果：Kind 为 Array 且 Items 不为空。
    [Fact]
    public void Array_ShouldCarryItemsSchema()
    {
        var schema = Param.Array(
            items: Param.Integer(required: false),
            description: "索引列表");

        Assert.Equal(ActionParamKind.Array, schema.Kind);
        Assert.NotNull(schema.Items);
        Assert.Equal(ActionParamKind.Integer, schema.Items!.Kind);
        Assert.Equal("索引列表", schema.Description);
    }

    // 测试点：对象参数工厂应支持嵌套数组与对象结构。
    // 预期结果：Properties 中的嵌套字段结构完整可读。
    [Fact]
    public void Object_ShouldBuildNestedSchema()
    {
        var schema = Param.Object(
            new Dictionary<string, ActionParamSchema>
            {
                ["query"] = Param.String(),
                ["filters"] = Param.Object(
                    new Dictionary<string, ActionParamSchema>
                    {
                        ["tags"] = Param.Array(Param.String(required: false), required: false),
                        ["strict"] = Param.Boolean(required: false, defaultValue: false)
                    },
                    required: false)
            });

        Assert.Equal(ActionParamKind.Object, schema.Kind);
        Assert.NotNull(schema.Properties);
        Assert.True(schema.Properties!.ContainsKey("query"));
        Assert.True(schema.Properties.ContainsKey("filters"));
        Assert.Equal(ActionParamKind.Object, schema.Properties["filters"].Kind);
        Assert.Equal(ActionParamKind.Array, schema.Properties["filters"].Properties!["tags"].Kind);
    }
}
