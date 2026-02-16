using ACI.Core.Models;
using ACI.Framework.Components;
using ACI.Framework.Runtime;

namespace ACI.Framework.BuiltIn;

/// <summary>
/// 内置应用启动器窗口。
/// </summary>
public class AppLauncher : ContextApp
{
    /// <summary>
    /// 获取当前可启动应用列表。
    /// </summary>
    private readonly Func<IEnumerable<(string Name, string? Description)>> _getApps;

    /// <summary>
    /// 创建启动器应用。
    /// </summary>
    public AppLauncher(Func<IEnumerable<(string Name, string? Description)>> getApps)
    {
        _getApps = getApps;
    }

    public override string Name => "launcher";

    public override string? AppDescription => "Browse and launch installed applications.";

    /// <summary>
    /// 注册命名空间工具定义。
    /// </summary>
    public override void OnCreate()
    {
        RegisterToolNamespace("launcher",
        [
            new ToolDescriptor
            {
                Id = "open",
                Params = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["app"] = "string"
                },
                Description = "Open an application by name."
            }
        ]);
    }

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
            NamespaceRefs = ["launcher"],
            Options = new WindowOptions
            {
                // 启动器常驻且不可关闭，同时在 prompt 裁剪中固定保留。
                Closable = false,
                PinInPrompt = true
            },
            Actions =
            [
                new ContextAction
                {
                    Id = "open",
                    Label = "Open App",
                    Params = Param.Object(new()
                    {
                        ["app"] = Param.String()
                    }),
                    Handler = async ctx =>
                    {
                        var appName = ctx.GetString("app");
                        if (string.IsNullOrEmpty(appName))
                        {
                            return ActionResult.Fail("Please specify an app name.");
                        }

                        // 返回 launch 指令，由 InteractionController 统一执行启动流程。
                        return ActionResult.Ok(
                            summary: $"Open app {appName}",
                            shouldClose: false,
                            data: new { action = "launch", app = appName, close_source = true }
                        );
                    }
                }
            ]
        };
    }

    /// <summary>
    /// 构建可用应用列表展示内容。
    /// </summary>
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
