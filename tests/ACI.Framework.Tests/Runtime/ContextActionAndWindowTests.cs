using ACI.Core.Abstractions;
using ACI.Core.Models;
using ACI.Framework.Components;
using ACI.Framework.Runtime;
using ACI.Tests.Common.TestData;

namespace ACI.Framework.Tests.Runtime;

public class ContextActionAndWindowTests
{
    // 测试点：AsAsync 应返回新的异步动作定义，且保留原有核心字段。
    // 预期结果：返回对象 Mode 为 Async，Id/Label/Params 与原动作一致。
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

    // 测试点：ToActionDefinition 应完整映射动作契约字段。
    // 预期结果：ActionDefinition 中 id/label/mode/params 全部可用。
    [Fact]
    public void ToActionDefinition_ShouldMapAllContractFields()
    {
        var action = new ContextAction
        {
            Id = "open",
            Label = "Open File",
            Mode = ActionExecutionMode.Async,
            Params = Param.Object(new Dictionary<string, ActionParamSchema>
            {
                ["path"] = Param.String()
            }),
            Handler = _ => Task.FromResult(ActionResult.Ok())
        };

        var definition = action.ToActionDefinition();

        Assert.Equal("open", definition.Id);
        Assert.Equal("Open File", definition.Label);
        Assert.Equal(ActionExecutionMode.Async, definition.Mode);
        Assert.NotNull(definition.ParamsSchema);
        Assert.Equal(ActionParamKind.Object, definition.ParamsSchema!.Kind);
    }

    // 测试点：ContextWindow.ToWindow 应构建 Core Window 并附带可执行 handler。
    // 预期结果：转换后窗口包含动作列表，且 handler 执行成功。
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

    // 测试点：当 actionId 不存在时，ContextActionHandler 应返回失败结果而非抛异常。
    // 预期结果：Success 为 false，错误信息包含请求的 actionId。
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

    // 测试点：参数对象应在 ActionContext 中按 JSON 结构可读取。
    // 预期结果：处理器可以读取到字符串参数并正常返回。
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
