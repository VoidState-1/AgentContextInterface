using ACI.Core.Models;
using ACI.Framework.Components;
using ACI.Framework.Runtime;

namespace ACI.Framework.Tests.Runtime;

public class ContextActionAndWindowTests
{
    [Fact]
    public void AsAsync_ShouldReturnCopiedActionWithAsyncMode()
    {
        var original = new ContextAction
        {
            Id = "search",
            Description = "Search",
            Params = Param.Object(new Dictionary<string, ActionParamSchema>
            {
                ["query"] = Param.String()
            }),
            Handler = _ => Task.FromResult(ActionResult.Ok())
        };

        var asyncAction = original.AsAsync();

        Assert.NotSame(original, asyncAction);
        Assert.Equal("search", asyncAction.Id);
        Assert.Equal("Search", asyncAction.Description);
        Assert.Equal(ActionExecutionMode.Async, asyncAction.Mode);
        Assert.Same(original.Params, asyncAction.Params);
    }

    [Fact]
    public void ToWindow_ShouldNotAttachHandlerByDefault()
    {
        var contextWindow = new ContextWindow
        {
            Id = "toolbox",
            Description = new Text("tools"),
            Content = new Text("content"),
            NamespaceRefs = ["demo", "system"]
        };

        var window = contextWindow.ToWindow();

        Assert.Equal("toolbox", window.Id);
        Assert.Null(window.Handler);
        Assert.Equal(2, window.NamespaceRefs.Count);
        Assert.Contains("demo", window.NamespaceRefs);
        Assert.Contains("system", window.NamespaceRefs);
    }
}
