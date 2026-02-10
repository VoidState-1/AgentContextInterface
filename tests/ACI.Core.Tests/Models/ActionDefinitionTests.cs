using ACI.Core.Models;

namespace ACI.Core.Tests.Models;

public class ActionDefinitionTests
{
    // 测试点：ToXml 应输出 id、params 与 async mode 属性。
    // 预期结果：XML 节点包含 id/params/mode 且文本为标签名。
    [Fact]
    public void ToXml_WithSchemaAndAsyncMode_ShouldContainExpectedAttributes()
    {
        var action = new ActionDefinition
        {
            Id = "search",
            Label = "Search",
            Mode = ActionExecutionMode.Async,
            ParamsSchema = new ActionParamSchema
            {
                Kind = ActionParamKind.String
            }
        };

        var xml = action.ToXml();

        Assert.Equal("action", xml.Name.LocalName);
        Assert.Equal("search", (string?)xml.Attribute("id"));
        Assert.Equal("string", (string?)xml.Attribute("params"));
        Assert.Equal("async", (string?)xml.Attribute("mode"));
        Assert.Equal("Search", xml.Value);
    }

    // 测试点：同步动作不应输出 mode 属性。
    // 预期结果：mode 属性不存在。
    [Fact]
    public void ToXml_SyncMode_ShouldNotContainModeAttribute()
    {
        var action = new ActionDefinition
        {
            Id = "open",
            Label = "Open",
            Mode = ActionExecutionMode.Sync
        };

        var xml = action.ToXml();

        Assert.Null(xml.Attribute("mode"));
    }
}

