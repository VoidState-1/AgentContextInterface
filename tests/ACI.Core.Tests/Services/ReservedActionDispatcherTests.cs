using ACI.Core.Models;
using ACI.Core.Services;
using ACI.Tests.Common.TestData;

namespace ACI.Core.Tests.Services;

public class ReservedActionDispatcherTests
{
    [Fact]
    public void TryDispatch_WithNonReservedAction_ShouldReturnFalse()
    {
        var dispatcher = new ReservedActionDispatcher();
        var window = new Window
        {
            Id = "w1",
            Content = new TextContent("content")
        };

        var handled = dispatcher.TryDispatch(window, "send", null, out var result);

        Assert.False(handled);
        Assert.Null(result);
    }

    [Fact]
    public void TryDispatch_CloseOnNonClosableWindow_ShouldReturnFail()
    {
        var dispatcher = new ReservedActionDispatcher();
        var window = new Window
        {
            Id = "w2",
            Content = new TextContent("content"),
            Options = new WindowOptions { Closable = false }
        };

        var handled = dispatcher.TryDispatch(window, "close", null, out var result);

        Assert.True(handled);
        Assert.False(result.Success);
        Assert.Equal("Window 'w2' cannot be closed", result.Message);
    }

    [Fact]
    public void TryDispatch_CloseWithSummary_ShouldReturnCloseResult()
    {
        var dispatcher = new ReservedActionDispatcher();
        var window = new Window
        {
            Id = "w3",
            Content = new TextContent("content")
        };
        var parameters = TestJson.Parse("""{"summary":"done"}""");

        var handled = dispatcher.TryDispatch(window, "close", parameters, out var result);

        Assert.True(handled);
        Assert.True(result.Success);
        Assert.True(result.ShouldClose);
        Assert.Equal("done", result.Summary);
    }
}
