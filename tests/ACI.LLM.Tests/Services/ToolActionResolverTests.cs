using ACI.Core.Models;
using ACI.Core.Services;
using ACI.LLM;
using ACI.LLM.Services;

namespace ACI.LLM.Tests.Services;

public class ToolActionResolverTests
{
    [Fact]
    public void Resolve_QualifiedActionId_ShouldResolveSuccessfully()
    {
        var registry = BuildRegistry();
        var window = new Window
        {
            Id = "w1",
            Content = new TextContent("content"),
            NamespaceRefs = ["mailbox"]
        };

        var parsed = new ParsedAction
        {
            WindowId = "w1",
            ActionId = "mailbox.send"
        };

        var resolved = ToolActionResolver.Resolve(parsed, window, registry);

        Assert.True(resolved.Success);
        Assert.NotNull(resolved.Action);
        Assert.Equal("mailbox", resolved.Action!.NamespaceId);
        Assert.Equal("send", resolved.Action.ToolId);
        Assert.Equal("mailbox.send", resolved.Action.QualifiedToolId);
    }

    [Fact]
    public void Resolve_ShortActionIdWithUniqueMatch_ShouldResolveSuccessfully()
    {
        var registry = BuildRegistry();
        var window = new Window
        {
            Id = "w1",
            Content = new TextContent("content"),
            NamespaceRefs = ["mailbox", "system"]
        };

        var parsed = new ParsedAction
        {
            WindowId = "w1",
            ActionId = "close"
        };

        var resolved = ToolActionResolver.Resolve(parsed, window, registry);

        Assert.True(resolved.Success);
        Assert.Equal("system", resolved.Action!.NamespaceId);
        Assert.Equal("close", resolved.Action.ToolId);
    }

    [Fact]
    public void Resolve_ShortActionIdWithMultipleMatches_ShouldFail()
    {
        var registry = new ToolNamespaceRegistry();
        registry.Upsert(new ToolNamespaceDefinition
        {
            Id = "a",
            Tools = [new ToolDescriptor { Id = "run", Description = "run a" }]
        });
        registry.Upsert(new ToolNamespaceDefinition
        {
            Id = "b",
            Tools = [new ToolDescriptor { Id = "run", Description = "run b" }]
        });

        var window = new Window
        {
            Id = "w1",
            Content = new TextContent("content"),
            NamespaceRefs = ["a", "b"]
        };

        var parsed = new ParsedAction
        {
            WindowId = "w1",
            ActionId = "run"
        };

        var resolved = ToolActionResolver.Resolve(parsed, window, registry);

        Assert.False(resolved.Success);
        Assert.Contains("Ambiguous", resolved.Error);
    }

    [Fact]
    public void Resolve_QualifiedActionIdWithInvisibleNamespace_ShouldFail()
    {
        var registry = BuildRegistry();
        var window = new Window
        {
            Id = "w1",
            Content = new TextContent("content"),
            NamespaceRefs = ["mailbox"]
        };

        var parsed = new ParsedAction
        {
            WindowId = "w1",
            ActionId = "system.close"
        };

        var resolved = ToolActionResolver.Resolve(parsed, window, registry);

        Assert.False(resolved.Success);
        Assert.Contains("not visible", resolved.Error);
    }

    private static ToolNamespaceRegistry BuildRegistry()
    {
        var registry = new ToolNamespaceRegistry();
        registry.Upsert(new ToolNamespaceDefinition
        {
            Id = "mailbox",
            Tools =
            [
                new ToolDescriptor
                {
                    Id = "send",
                    Description = "send",
                    Params = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["to"] = "string",
                        ["content"] = "string"
                    }
                }
            ]
        });

        registry.Upsert(new ToolNamespaceDefinition
        {
            Id = "system",
            Tools =
            [
                new ToolDescriptor
                {
                    Id = "close",
                    Description = "close"
                }
            ]
        });

        return registry;
    }
}
