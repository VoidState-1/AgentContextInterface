using ACI.Core.Models;

namespace ACI.Tests.Common.TestData;

public sealed class WindowBuilder
{
    private readonly Window _window;

    public WindowBuilder(string? windowId = null)
    {
        _window = new Window
        {
            Id = windowId ?? TestIds.Window(),
            Content = new TextContent("test-content")
        };
    }

    public WindowBuilder WithApp(string appName)
    {
        _window.AppName = appName;
        return this;
    }

    public WindowBuilder WithDescription(string description)
    {
        _window.Description = new TextContent(description);
        return this;
    }

    public WindowBuilder AddAction(ActionDefinition action)
    {
        _window.Actions.Add(action);
        return this;
    }

    public Window Build()
    {
        return _window;
    }
}

