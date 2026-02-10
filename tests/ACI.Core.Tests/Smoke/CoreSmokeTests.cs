using ACI.Core.Models;
using ACI.Tests.Common.TestData;

namespace ACI.Core.Tests.Smoke;

public class CoreSmokeTests
{
    [Fact]
    public void ActionResult_Ok_ShouldBeSuccessful()
    {
        var result = ActionResult.Ok(message: "ok");

        Assert.True(result.Success);
        Assert.Equal("ok", result.Message);
    }

    [Fact]
    public void WindowBuilder_DefaultBuild_ShouldProduceWindow()
    {
        var window = new WindowBuilder()
            .WithApp("test-app")
            .WithDescription("test-desc")
            .Build();

        Assert.Equal("test-app", window.AppName);
        Assert.NotNull(window.Description);
        Assert.NotEmpty(window.Render());
    }
}

