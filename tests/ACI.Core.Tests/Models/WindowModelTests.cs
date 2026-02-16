using ACI.Core.Models;

namespace ACI.Core.Tests.Models;

public class WindowModelTests
{
    [Fact]
    public void Render_CompactMode_ShouldOutputCompactWindowXml()
    {
        var window = new Window
        {
            Id = "w-compact",
            Content = new TextContent("compact-content"),
            Description = new TextContent("desc"),
            NamespaceRefs = ["system"],
            Options = new WindowOptions
            {
                RenderMode = RenderMode.Compact
            }
        };

        var xml = window.Render();

        Assert.Contains("compact=\"true\"", xml);
        Assert.Contains("ns=\"system\"", xml);
        Assert.Contains("compact-content", xml);
        Assert.DoesNotContain("<description>", xml);
    }

    [Fact]
    public void Render_FullModeWithVisibleMeta_ShouldContainSectionsAndNamespaceAttr()
    {
        var window = new Window
        {
            Id = "w-full",
            Content = new TextContent("full-content"),
            Description = new TextContent("full-desc"),
            NamespaceRefs = ["mailbox", "system"],
            Meta = new WindowMeta
            {
                Hidden = false,
                Tokens = 9,
                CreatedAt = 1,
                UpdatedAt = 2
            }
        };

        var xml = window.Render();

        Assert.Contains("<meta>", xml);
        Assert.Contains("<description>full-desc</description>", xml);
        Assert.Contains("<content>full-content</content>", xml);
        Assert.Contains("ns=\"mailbox,system\"", xml);
        Assert.DoesNotContain("<actions>", xml);
    }
}
