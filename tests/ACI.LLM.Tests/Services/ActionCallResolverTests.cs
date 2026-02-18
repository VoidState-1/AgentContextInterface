using ACI.Core.Models;
using ACI.Core.Services;
using ACI.LLM;
using ACI.LLM.Services;

namespace ACI.LLM.Tests.Services;

public class ActionCallResolverTests
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

        var resolved = ActionCallResolver.Resolve(parsed, window, registry);

        Assert.True(resolved.Success);
        Assert.NotNull(resolved.Action);
        Assert.Equal("mailbox", resolved.Action!.NamespaceId);
        Assert.Equal("send", resolved.Action.ActionId);
        Assert.Equal("mailbox.send", resolved.Action.QualifiedActionId);
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

        var resolved = ActionCallResolver.Resolve(parsed, window, registry);

        Assert.True(resolved.Success);
        Assert.Equal("system", resolved.Action!.NamespaceId);
        Assert.Equal("close", resolved.Action.ActionId);
    }

    [Fact]
    public void Resolve_ShortActionIdWithMultipleMatches_ShouldFail()
    {
        var registry = new ActionNamespaceRegistry();
        registry.Upsert(new ActionNamespaceDefinition
        {
            Id = "a",
            Actions = [new ActionDescriptor { Id = "run", Description = "run a" }]
        });
        registry.Upsert(new ActionNamespaceDefinition
        {
            Id = "b",
            Actions = [new ActionDescriptor { Id = "run", Description = "run b" }]
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

        var resolved = ActionCallResolver.Resolve(parsed, window, registry);

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

        var resolved = ActionCallResolver.Resolve(parsed, window, registry);

        Assert.False(resolved.Success);
        Assert.Contains("not visible", resolved.Error);
    }

    private static ActionNamespaceRegistry BuildRegistry()
    {
        var registry = new ActionNamespaceRegistry();
        registry.Upsert(new ActionNamespaceDefinition
        {
            Id = "mailbox",
            Actions =
            [
                new ActionDescriptor
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

        registry.Upsert(new ActionNamespaceDefinition
        {
            Id = "system",
            Actions =
            [
                new ActionDescriptor
                {
                    Id = "close",
                    Description = "close"
                }
            ]
        });

        return registry;
    }
}
