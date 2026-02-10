using ACI.Core.Models;
using ACI.Core.Abstractions;
using ACI.Core.Services;
using ACI.Tests.Common.Fakes;
using ACI.Tests.Common.TestData;

namespace ACI.Core.Tests.Services;

public class ActionExecutorTests
{
    // 测试点：目标窗口不存在时应直接失败，不触发后续执行逻辑。
    // 预期结果：返回失败结果，消息为窗口不存在。
    [Fact]
    public async Task ExecuteAsync_WindowNotFound_ShouldFail()
    {
        var clock = new FakeSeqClock();
        var windows = new WindowManager(clock);
        var events = new SpyEventBus();
        var executor = new ActionExecutor(windows, clock, events);

        var result = await executor.ExecuteAsync("missing-window", "any-action");

        Assert.False(result.Success);
        Assert.Equal("Window 'missing-window' does not exist", result.Message);
        Assert.Empty(events.PublishedEvents);
    }

    // 测试点：不可关闭窗口执行 close 时应被拒绝。
    // 预期结果：返回失败结果，窗口仍保留。
    [Fact]
    public async Task ExecuteAsync_CloseOnNonClosableWindow_ShouldFail()
    {
        var clock = new FakeSeqClock();
        var windows = new WindowManager(clock);
        var events = new SpyEventBus();
        var window = new Window
        {
            Id = "w-1",
            Content = new TextContent("content"),
            Options = new WindowOptions
            {
                Closable = false
            }
        };
        windows.Add(window);

        var executor = new ActionExecutor(windows, clock, events);
        var result = await executor.ExecuteAsync("w-1", "close");

        Assert.False(result.Success);
        Assert.Equal("Window 'w-1' cannot be closed", result.Message);
        Assert.NotNull(windows.Get("w-1"));
        Assert.Empty(events.PublishedEvents);
    }

    // 测试点：close 动作成功时应移除窗口并发布事件，summary 正确提取。
    // 预期结果：返回 ShouldClose=true，窗口被删除，事件字段与结果一致。
    [Fact]
    public async Task ExecuteAsync_CloseSuccess_ShouldRemoveWindowAndPublishEvent()
    {
        var clock = new FakeSeqClock();
        var windows = new WindowManager(clock);
        var events = new SpyEventBus();
        windows.Add(new Window
        {
            Id = "w-close",
            Content = new TextContent("content")
        });

        var executor = new ActionExecutor(windows, clock, events);
        var parameters = TestJson.Parse("""{"summary":"closed by test"}""");
        var result = await executor.ExecuteAsync("w-close", "close", parameters);

        Assert.True(result.Success);
        Assert.True(result.ShouldClose);
        Assert.Equal("closed by test", result.Summary);
        Assert.Equal(1, result.LogSeq);
        Assert.Null(windows.Get("w-close"));

        var evt = Assert.IsType<ActionExecutedEvent>(Assert.Single(events.PublishedEvents));
        Assert.Equal("w-close", evt.WindowId);
        Assert.Equal("close", evt.ActionId);
        Assert.True(evt.Success);
        Assert.Equal("closed by test", evt.Summary);
        Assert.Equal(result.LogSeq, evt.Seq);
    }

    // 测试点：窗口上不存在目标动作时应直接失败。
    // 预期结果：返回动作不存在错误，不发布执行事件。
    [Fact]
    public async Task ExecuteAsync_ActionNotFound_ShouldFail()
    {
        var clock = new FakeSeqClock();
        var windows = new WindowManager(clock);
        var events = new SpyEventBus();
        windows.Add(new Window
        {
            Id = "w-2",
            Content = new TextContent("content")
        });

        var executor = new ActionExecutor(windows, clock, events);
        var result = await executor.ExecuteAsync("w-2", "missing-action");

        Assert.False(result.Success);
        Assert.Equal("Action 'missing-action' does not exist on window 'w-2'", result.Message);
        Assert.Empty(events.PublishedEvents);
    }

    // 测试点：参数校验失败时应阻止 handler 执行。
    // 预期结果：返回校验错误，handler 调用次数为 0。
    [Fact]
    public async Task ExecuteAsync_InvalidParams_ShouldFailAndNotInvokeHandler()
    {
        var clock = new FakeSeqClock();
        var windows = new WindowManager(clock);
        var events = new SpyEventBus();
        var handler = new FakeActionHandler();

        windows.Add(new Window
        {
            Id = "w-3",
            Content = new TextContent("content"),
            Handler = handler,
            Actions =
            [
                new ActionDefinition
                {
                    Id = "submit",
                    Label = "Submit",
                    ParamsSchema = new ActionParamSchema
                    {
                        Kind = ActionParamKind.Object,
                        Properties = new Dictionary<string, ActionParamSchema>
                        {
                            ["name"] = new ActionParamSchema
                            {
                                Kind = ActionParamKind.String,
                                Required = true
                            }
                        }
                    }
                }
            ]
        });

        var executor = new ActionExecutor(windows, clock, events);
        var result = await executor.ExecuteAsync("w-3", "submit", TestJson.Parse("{}"));

        Assert.False(result.Success);
        Assert.Equal("params.name is required", result.Message);
        Assert.Empty(handler.Calls);
        Assert.Empty(events.PublishedEvents);
    }

    // 测试点：handler 成功执行时应返回成功结果并发布成功事件。
    // 预期结果：result.Success=true，事件 Success=true，LogSeq 与事件 Seq 对齐。
    [Fact]
    public async Task ExecuteAsync_HandlerSuccess_ShouldPublishSuccessEvent()
    {
        var clock = new FakeSeqClock();
        var windows = new WindowManager(clock);
        var events = new SpyEventBus();
        var handler = new FakeActionHandler(_ =>
            Task.FromResult(ActionResult.Ok(summary: "done")));

        windows.Add(new Window
        {
            Id = "w-4",
            Content = new TextContent("content"),
            Handler = handler,
            Actions =
            [
                new ActionDefinition
                {
                    Id = "run",
                    Label = "Run"
                }
            ]
        });

        var executor = new ActionExecutor(windows, clock, events);
        var result = await executor.ExecuteAsync("w-4", "run");

        Assert.True(result.Success);
        Assert.Equal("done", result.Summary);
        Assert.Equal(1, result.LogSeq);

        var evt = Assert.IsType<ActionExecutedEvent>(Assert.Single(events.PublishedEvents));
        Assert.True(evt.Success);
        Assert.Equal("run", evt.ActionId);
        Assert.Equal(result.LogSeq, evt.Seq);
    }

    // 测试点：handler 抛异常时应被捕获并转为失败结果。
    // 预期结果：返回失败结果，消息包含异常信息，并发布失败事件。
    [Fact]
    public async Task ExecuteAsync_HandlerThrows_ShouldReturnFailResult()
    {
        var clock = new FakeSeqClock();
        var windows = new WindowManager(clock);
        var events = new SpyEventBus();
        var handler = new FakeActionHandler(_ =>
            throw new InvalidOperationException("boom"));

        windows.Add(new Window
        {
            Id = "w-5",
            Content = new TextContent("content"),
            Handler = handler,
            Actions =
            [
                new ActionDefinition
                {
                    Id = "run",
                    Label = "Run"
                }
            ]
        });

        var executor = new ActionExecutor(windows, clock, events);
        var result = await executor.ExecuteAsync("w-5", "run");

        Assert.False(result.Success);
        Assert.Contains("Action execution failed: boom", result.Message);
        var evt = Assert.IsType<ActionExecutedEvent>(Assert.Single(events.PublishedEvents));
        Assert.False(evt.Success);
    }

    // 测试点：结果要求关闭窗口时应删除窗口。
    // 预期结果：执行后窗口不存在。
    [Fact]
    public async Task ExecuteAsync_ResultShouldClose_ShouldRemoveWindow()
    {
        var clock = new FakeSeqClock();
        var windows = new WindowManager(clock);
        var events = new SpyEventBus();
        var handler = new FakeActionHandler(_ =>
            Task.FromResult(ActionResult.Ok(shouldRefresh: false, shouldClose: true)));

        windows.Add(new Window
        {
            Id = "w-6",
            Content = new TextContent("content"),
            Handler = handler,
            Actions =
            [
                new ActionDefinition
                {
                    Id = "finish",
                    Label = "Finish"
                }
            ]
        });

        var executor = new ActionExecutor(windows, clock, events);
        var result = await executor.ExecuteAsync("w-6", "finish");

        Assert.True(result.Success);
        Assert.Null(windows.Get("w-6"));
    }

    // 测试点：窗口配置 AutoCloseOnAction=true 时应在动作后自动关闭。
    // 预期结果：即使结果未要求关闭，窗口也会被移除。
    [Fact]
    public async Task ExecuteAsync_AutoCloseOnAction_ShouldRemoveWindow()
    {
        var clock = new FakeSeqClock();
        var windows = new WindowManager(clock);
        var events = new SpyEventBus();
        var handler = new FakeActionHandler(_ =>
            Task.FromResult(ActionResult.Ok(shouldRefresh: false, shouldClose: false)));

        windows.Add(new Window
        {
            Id = "w-7",
            Content = new TextContent("content"),
            Options = new WindowOptions
            {
                AutoCloseOnAction = true
            },
            Handler = handler,
            Actions =
            [
                new ActionDefinition
                {
                    Id = "save",
                    Label = "Save"
                }
            ]
        });

        var executor = new ActionExecutor(windows, clock, events);
        var result = await executor.ExecuteAsync("w-7", "save");

        Assert.True(result.Success);
        Assert.Null(windows.Get("w-7"));
    }

    // 测试点：提供 refreshWindow 回调时，刷新应走回调分支。
    // 预期结果：回调收到目标窗口 ID。
    [Fact]
    public async Task ExecuteAsync_WithRefreshCallback_ShouldInvokeCallback()
    {
        var clock = new FakeSeqClock();
        var windows = new WindowManager(clock);
        var events = new SpyEventBus();
        var refreshed = new List<string>();

        windows.Add(new Window
        {
            Id = "w-8",
            Content = new TextContent("content"),
            Actions =
            [
                new ActionDefinition
                {
                    Id = "refresh",
                    Label = "Refresh"
                }
            ]
        });

        var executor = new ActionExecutor(
            windows,
            clock,
            events,
            windowId => refreshed.Add(windowId));

        var result = await executor.ExecuteAsync("w-8", "refresh");

        Assert.True(result.Success);
        Assert.Single(refreshed);
        Assert.Equal("w-8", refreshed[0]);
    }

    // 测试点：未提供回调时，刷新应回退到 WindowManager.NotifyUpdated。
    // 预期结果：触发一次 Updated 事件。
    [Fact]
    public async Task ExecuteAsync_WithoutRefreshCallback_ShouldUseWindowManagerNotifyUpdated()
    {
        var clock = new FakeSeqClock();
        var windows = new WindowManager(clock);
        var events = new SpyEventBus();
        var changedEvents = new List<WindowChangedEvent>();

        windows.OnChanged += evt => changedEvents.Add(evt);
        windows.Add(new Window
        {
            Id = "w-9",
            Content = new TextContent("content"),
            Actions =
            [
                new ActionDefinition
                {
                    Id = "refresh",
                    Label = "Refresh"
                }
            ]
        });
        changedEvents.Clear();

        var executor = new ActionExecutor(windows, clock, events);
        var result = await executor.ExecuteAsync("w-9", "refresh");

        Assert.True(result.Success);
        var evt = Assert.Single(changedEvents);
        Assert.Equal(WindowEventType.Updated, evt.Type);
        Assert.Equal("w-9", evt.WindowId);
    }
}
