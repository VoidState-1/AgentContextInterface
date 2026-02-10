using ACI.Core.Models;

namespace ACI.Core.Tests.Models;

public class WindowModelTests
{
    // 测试点：Compact 模式应仅输出窗口核心内容，不展开 description/actions。
    // 预期结果：XML 含 compact=true，且不含 description/actions 标签。
    [Fact]
    public void Render_CompactMode_ShouldOutputCompactWindowXml()
    {
        var window = new Window
        {
            Id = "w-compact",
            Content = new TextContent("compact-content"),
            Description = new TextContent("desc"),
            Options = new WindowOptions
            {
                RenderMode = RenderMode.Compact
            },
            Actions =
            [
                new ActionDefinition
                {
                    Id = "act",
                    Label = "Act"
                }
            ]
        };

        var xml = window.Render();

        Assert.Contains("compact=\"true\"", xml);
        Assert.Contains("compact-content", xml);
        Assert.DoesNotContain("<description>", xml);
        Assert.DoesNotContain("<actions>", xml);
    }

    // 测试点：Full 模式在 Meta 可见时应输出 meta/description/content/actions。
    // 预期结果：完整结构标签均存在。
    [Fact]
    public void Render_FullModeWithVisibleMeta_ShouldContainAllSections()
    {
        var window = new Window
        {
            Id = "w-full",
            Content = new TextContent("full-content"),
            Description = new TextContent("full-desc"),
            Meta = new WindowMeta
            {
                Hidden = false,
                Tokens = 9,
                CreatedAt = 1,
                UpdatedAt = 2
            },
            Actions =
            [
                new ActionDefinition
                {
                    Id = "act",
                    Label = "Act"
                }
            ]
        };

        var xml = window.Render();

        Assert.Contains("<meta>", xml);
        Assert.Contains("<description>full-desc</description>", xml);
        Assert.Contains("<content>full-content</content>", xml);
        Assert.Contains("<actions>", xml);
    }
}

