using ACI.Core.Models;

namespace ACI.Core.Tests.Models;

public class ActionResultTests
{
    // 测试点：Ok 工厂方法默认值应符合成功动作语义。
    // 预期结果：Success=true，ShouldRefresh=true，ShouldClose=false。
    [Fact]
    public void Ok_Defaults_ShouldRepresentSuccessfulRefreshableResult()
    {
        var result = ActionResult.Ok();

        Assert.True(result.Success);
        Assert.True(result.ShouldRefresh);
        Assert.False(result.ShouldClose);
    }

    // 测试点：Fail 工厂方法应生成稳定失败语义。
    // 预期结果：Success=false，ShouldRefresh=false，ShouldClose=false。
    [Fact]
    public void Fail_ShouldRepresentStableFailureResult()
    {
        var result = ActionResult.Fail("bad request");

        Assert.False(result.Success);
        Assert.Equal("bad request", result.Message);
        Assert.False(result.ShouldRefresh);
        Assert.False(result.ShouldClose);
    }

    // 测试点：Close 工厂方法应生成关闭窗口语义。
    // 预期结果：Success=true，ShouldClose=true，ShouldRefresh=false。
    [Fact]
    public void Close_ShouldRepresentWindowCloseResult()
    {
        var result = ActionResult.Close("closing");

        Assert.True(result.Success);
        Assert.Equal("closing", result.Summary);
        Assert.True(result.ShouldClose);
        Assert.False(result.ShouldRefresh);
    }
}

