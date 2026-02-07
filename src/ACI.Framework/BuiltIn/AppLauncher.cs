using ACI.Core.Models;
using ACI.Framework.Components;
using ACI.Framework.Runtime;

namespace ACI.Framework.BuiltIn;

public class AppLauncher : ContextApp
{
    private readonly Func<IEnumerable<(string Name, string? Description)>> _getApps;

    public AppLauncher(Func<IEnumerable<(string Name, string? Description)>> getApps)
    {
        _getApps = getApps;
    }

    public override string Name => "launcher";

    public override string? AppDescription => "Browse and launch installed applications.";

    public override ContextWindow CreateWindow(string? intent)
    {
        return new ContextWindow
        {
            Id = "launcher",
            Description = new Text("Application launcher. Select an app to open."),
            Content = new VStack
            {
                Children = BuildAppList()
            },
            Options = new WindowOptions
            {
                Closable = false,
                PinInPrompt = true
            },
            Actions =
            [
                new ContextAction
                {
                    Id = "open",
                    Label = "Open App",
                    Handler = async ctx =>
                    {
                        var appName = ctx.GetString("app");
                        if (string.IsNullOrEmpty(appName))
                        {
                            return ActionResult.Fail("Please specify an app name.");
                        }

                        return ActionResult.Ok(
                            summary: $"Open app {appName}",
                            shouldClose: false,
                            data: new { action = "launch", app = appName, close_source = true }
                        );
                    }
                }.WithParam("app", ParamType.String)
            ]
        };
    }

    private List<IComponent> BuildAppList()
    {
        var apps = _getApps().ToList();
        var components = new List<IComponent>
        {
            new Text("Installed applications:"),
            new Text("")
        };

        for (var index = 0; index < apps.Count; index++)
        {
            var (name, description) = apps[index];
            var line = $"{index + 1}. {name}";
            if (!string.IsNullOrEmpty(description))
            {
                line += $" - {description}";
            }
            components.Add(new Text(line));
        }

        return components;
    }
}