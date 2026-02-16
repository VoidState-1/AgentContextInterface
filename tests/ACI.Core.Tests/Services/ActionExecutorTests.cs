using ACI.Core.Abstractions;
using ACI.Core.Models;
using ACI.Core.Services;
using ACI.Tests.Common.Fakes;
using ACI.Tests.Common.TestData;

namespace ACI.Core.Tests.Services;

public class ActionExecutorTests
{
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

    [Fact]
    public async Task ExecuteAsync_CloseOnNonClosableWindow_ShouldFail()
    {
        var clock = new FakeSeqClock();
        var windows = new WindowManager(clock);
        var events = new SpyEventBus();
        windows.Add(new Window
        {
            Id = "w-1",
            Content = new TextContent("content"),
            Options = new WindowOptions { Closable = false }
        });

        var executor = new ActionExecutor(windows, clock, events);
        var result = await executor.ExecuteAsync("w-1", "close");

        Assert.False(result.Success);
        Assert.Equal("Window 'w-1' cannot be closed", result.Message);
        Assert.NotNull(windows.Get("w-1"));
        Assert.Empty(events.PublishedEvents);
    }

    [Fact]
    public async Task ExecuteAsync_CloseWithQualifiedActionId_ShouldCloseWindow()
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
        var result = await executor.ExecuteAsync("w-close", "system.close", parameters);

        Assert.True(result.Success);
        Assert.True(result.ShouldClose);
        Assert.Equal("closed by test", result.Summary);
        Assert.Equal(1, result.LogSeq);
        Assert.Null(windows.Get("w-close"));

        var evt = Assert.IsType<ActionExecutedEvent>(Assert.Single(events.PublishedEvents));
        Assert.Equal("w-close", evt.WindowId);
        Assert.Equal("system.close", evt.ActionId);
        Assert.True(evt.Success);
        Assert.Equal("closed by test", evt.Summary);
    }

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
        var evt = Assert.IsType<ActionExecutedEvent>(Assert.Single(events.PublishedEvents));
        Assert.False(evt.Success);
        Assert.Equal("missing-action", evt.ActionId);
    }

    [Fact]
    public async Task ExecuteAsync_HandlerFailure_ShouldPropagateResult()
    {
        var clock = new FakeSeqClock();
        var windows = new WindowManager(clock);
        var events = new SpyEventBus();
        var handler = new FakeActionHandler(_ => Task.FromResult(ActionResult.Fail("params.name is required")));

        windows.Add(new Window
        {
            Id = "w-3",
            Content = new TextContent("content"),
            Handler = handler
        });

        var executor = new ActionExecutor(windows, clock, events);
        var result = await executor.ExecuteAsync("w-3", "submit", TestJson.Parse("{}"));

        Assert.False(result.Success);
        Assert.Equal("params.name is required", result.Message);
        Assert.Single(handler.Calls);
        Assert.Single(events.PublishedEvents);
    }

    [Fact]
    public async Task ExecuteAsync_HandlerSuccess_ShouldPublishSuccessEvent()
    {
        var clock = new FakeSeqClock();
        var windows = new WindowManager(clock);
        var events = new SpyEventBus();
        var handler = new FakeActionHandler(_ => Task.FromResult(ActionResult.Ok(summary: "done")));

        windows.Add(new Window
        {
            Id = "w-4",
            Content = new TextContent("content"),
            Handler = handler
        });

        var executor = new ActionExecutor(windows, clock, events);
        var result = await executor.ExecuteAsync("w-4", "run");

        Assert.True(result.Success);
        Assert.Equal("done", result.Summary);
        Assert.Equal(1, result.LogSeq);

        var evt = Assert.IsType<ActionExecutedEvent>(Assert.Single(events.PublishedEvents));
        Assert.True(evt.Success);
        Assert.Equal("run", evt.ActionId);
    }

    [Fact]
    public async Task ExecuteAsync_NamespaceActionId_ShouldPassOriginalActionIdToHandler()
    {
        var clock = new FakeSeqClock();
        var windows = new WindowManager(clock);
        var events = new SpyEventBus();
        var handler = new FakeActionHandler();

        windows.Add(new Window
        {
            Id = "w-4b",
            Content = new TextContent("content"),
            Handler = handler
        });

        var executor = new ActionExecutor(windows, clock, events);
        var result = await executor.ExecuteAsync("w-4b", "mailbox.send");

        Assert.True(result.Success);
        var call = Assert.Single(handler.Calls);
        Assert.Equal("mailbox.send", call.ActionId);
    }

    [Fact]
    public async Task ExecuteAsync_HandlerThrows_ShouldReturnFailResult()
    {
        var clock = new FakeSeqClock();
        var windows = new WindowManager(clock);
        var events = new SpyEventBus();
        var handler = new FakeActionHandler(_ => throw new InvalidOperationException("boom"));

        windows.Add(new Window
        {
            Id = "w-5",
            Content = new TextContent("content"),
            Handler = handler
        });

        var executor = new ActionExecutor(windows, clock, events);
        var result = await executor.ExecuteAsync("w-5", "run");

        Assert.False(result.Success);
        Assert.Contains("Action execution failed: boom", result.Message);
        var evt = Assert.IsType<ActionExecutedEvent>(Assert.Single(events.PublishedEvents));
        Assert.False(evt.Success);
    }

    [Fact]
    public async Task ExecuteAsync_ResultShouldClose_ShouldRemoveWindow()
    {
        var clock = new FakeSeqClock();
        var windows = new WindowManager(clock);
        var events = new SpyEventBus();
        var handler = new FakeActionHandler(_ => Task.FromResult(ActionResult.Ok(shouldClose: true)));

        windows.Add(new Window
        {
            Id = "w-6",
            Content = new TextContent("content"),
            Handler = handler
        });

        var executor = new ActionExecutor(windows, clock, events);
        var result = await executor.ExecuteAsync("w-6", "finish");

        Assert.True(result.Success);
        Assert.Null(windows.Get("w-6"));
    }

    [Fact]
    public async Task ExecuteAsync_AutoCloseOnAction_ShouldRemoveWindow()
    {
        var clock = new FakeSeqClock();
        var windows = new WindowManager(clock);
        var events = new SpyEventBus();
        var handler = new FakeActionHandler(_ => Task.FromResult(ActionResult.Ok()));

        windows.Add(new Window
        {
            Id = "w-7",
            Content = new TextContent("content"),
            Options = new WindowOptions { AutoCloseOnAction = true },
            Handler = handler
        });

        var executor = new ActionExecutor(windows, clock, events);
        var result = await executor.ExecuteAsync("w-7", "save");

        Assert.True(result.Success);
        Assert.Null(windows.Get("w-7"));
    }

    [Fact]
    public async Task ExecuteAsync_WithRefreshCallback_ShouldInvokeCallback()
    {
        var clock = new FakeSeqClock();
        var windows = new WindowManager(clock);
        var events = new SpyEventBus();
        var refreshed = new List<string>();

        var handler = new FakeActionHandler(_ => Task.FromResult(ActionResult.Ok(shouldRefresh: true)));
        windows.Add(new Window
        {
            Id = "w-8",
            Content = new TextContent("content"),
            Handler = handler
        });

        var executor = new ActionExecutor(windows, clock, events, windowId => refreshed.Add(windowId));
        var result = await executor.ExecuteAsync("w-8", "refresh");

        Assert.True(result.Success);
        Assert.Single(refreshed);
        Assert.Equal("w-8", refreshed[0]);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutRefreshCallback_ShouldUseWindowManagerNotifyUpdated()
    {
        var clock = new FakeSeqClock();
        var windows = new WindowManager(clock);
        var events = new SpyEventBus();
        var changedEvents = new List<WindowChangedEvent>();

        var handler = new FakeActionHandler(_ => Task.FromResult(ActionResult.Ok(shouldRefresh: true)));
        windows.OnChanged += evt => changedEvents.Add(evt);
        windows.Add(new Window
        {
            Id = "w-9",
            Content = new TextContent("content"),
            Handler = handler
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
