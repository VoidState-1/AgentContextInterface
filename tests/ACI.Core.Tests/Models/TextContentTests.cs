using ACI.Core.Models;

namespace ACI.Core.Tests.Models;

public class TextContentTests
{
    // 测试点：静态文本构造应稳定返回原始文本。
    // 预期结果：Render 返回传入文本。
    [Fact]
    public void Render_StaticText_ShouldReturnOriginalText()
    {
        var text = new TextContent("hello");

        var rendered = text.Render();

        Assert.Equal("hello", rendered);
    }

    // 测试点：动态文本构造应在每次 Render 时重新求值。
    // 预期结果：连续两次渲染返回不同值。
    [Fact]
    public void Render_DynamicText_ShouldEvaluateFactoryEachTime()
    {
        var count = 0;
        var text = new TextContent(() =>
        {
            count++;
            return $"v-{count}";
        });

        var first = text.Render();
        var second = text.Render();

        Assert.Equal("v-1", first);
        Assert.Equal("v-2", second);
    }
}

