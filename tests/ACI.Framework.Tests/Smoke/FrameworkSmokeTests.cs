using ACI.Core.Models;
using ACI.Framework.Runtime;
using ACI.Tests.Common.Fakes;

namespace ACI.Framework.Tests.Smoke;

public class FrameworkSmokeTests
{
    [Fact]
    public void Param_ObjectSchema_ShouldBuildNestedSchema()
    {
        var schema = Param.Object(
            new Dictionary<string, ActionParamSchema>
            {
                ["query"] = Param.String(),
                ["options"] = Param.Object(
                    new Dictionary<string, ActionParamSchema>
                    {
                        ["recursive"] = Param.Boolean(required: false, defaultValue: false)
                    },
                    required: false)
            });

        Assert.Equal(ActionParamKind.Object, schema.Kind);
        Assert.NotNull(schema.Properties);
        Assert.True(schema.Properties.ContainsKey("query"));
        Assert.True(schema.Properties.ContainsKey("options"));
    }

    [Fact]
    public void FakeSeqClock_Next_ShouldIncrement()
    {
        var clock = new FakeSeqClock(seed: 10);

        var next = clock.Next();

        Assert.Equal(11, next);
        Assert.Equal(11, clock.CurrentSeq);
    }
}

