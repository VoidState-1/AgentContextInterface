using ACI.Core.Abstractions;
using ACI.Core.Models;
using ACI.Framework.Components;
using ACI.Framework.Runtime;
using ACI.Tests.Common.TestData;

namespace ACI.Framework.Tests.Runtime;

public class ContextActionAndWindowTests
{
    [Fact]
    public void AsAsync_ShouldReturnCopiedActionWithAsyncMode()
    {
        var original = new ContextAction
        {
            Id = "search",
            Label = "Search",
            Params = Param.Object(new Dictionary<string, ActionParamSchema>
            {
                ["query"] = Param.String()
            }),
            Handler = _ => Task.FromResult(ActionResult.Ok())
        };

        var asyncAction = original.AsAsync();

        Assert.NotSame(original, asyncAction);
        Assert.Equal("search", asyncAction.Id);
        Assert.Equal("Search", asyncAction.Label);
        Assert.Equal(ActionExecutionMode.Async, asyncAction.Mode);
        Assert.Same(original.Params, asyncAction.Params);
    }

    [Fact]
    public async Task ToWindow_ShouldCreateCoreWindowWithExecutableHandler()
    {
        var contextWindow = new ContextWindow
        {
            Id = "toolbox",
            Description = new Text("tools"),
            Content = new Text("content"),
            Actions =
            [
                new ContextAction
                {
                    Id = "ping",
                    Label = "Ping",
                    Handler = _ => Task.FromResult(ActionResult.Ok(message: "pong"))
                }
            ]
        };

        var window = contextWindow.ToWindow();
        var result = await window.Handler!.ExecuteAsync(new ActionContext
        {
            Window = window,
            ActionId = "ping"
        });

        Assert.Equal("toolbox", window.Id);
        Assert.NotNull(window.Handler);
        Assert.True(result.Success);
        Assert.Equal("pong", result.Message);
    }

    [Fact]
    public async Task Handler_WithUnknownActionId_ShouldReturnFailResult()
    {
        var contextWindow = new ContextWindow
        {
            Id = "toolbox",
            Content = new Text("content"),
            Actions =
            [
                new ContextAction
                {
                    Id = "known",
                    Label = "Known",
                    Handler = _ => Task.FromResult(ActionResult.Ok())
                }
            ]
        };

        var window = contextWindow.ToWindow();
        var result = await window.Handler!.ExecuteAsync(new ActionContext
        {
            Window = window,
            ActionId = "missing"
        });

        Assert.False(result.Success);
        Assert.NotNull(result.Message);
        Assert.Contains("missing", result.Message);
    }

    [Fact]
    public async Task Handler_ShouldReadObjectParametersFromActionContext()
    {
        var contextWindow = new ContextWindow
        {
            Id = "toolbox",
            Content = new Text("content"),
            Actions =
            [
                new ContextAction
                {
                    Id = "echo",
                    Label = "Echo",
                    Params = Param.Object(new Dictionary<string, ActionParamSchema>
                    {
                        ["name"] = Param.String()
                    }),
                    Handler = ctx =>
                    {
                        var name = ctx.GetString("name");
                        return Task.FromResult(ActionResult.Ok(message: name));
                    }
                }
            ]
        };

        var window = contextWindow.ToWindow();
        var result = await window.Handler!.ExecuteAsync(new ActionContext
        {
            Window = window,
            ActionId = "echo",
            Parameters = TestJson.Parse("""{"name":"aci"}""")
        });

        Assert.True(result.Success);
        Assert.Equal("aci", result.Message);
    }
}
