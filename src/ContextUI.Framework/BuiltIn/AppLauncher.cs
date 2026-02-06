using ContextUI.Core.Models;
using ContextUI.Framework.Components;
using ContextUI.Framework.Runtime;

namespace ContextUI.Framework.BuiltIn;

/// <summary>
/// 应用启动器 - 内置应用
/// </summary>
public class AppLauncher : ContextApp
{
    private readonly Func<IEnumerable<(string Name, string? Description)>> _getApps;

    public AppLauncher(Func<IEnumerable<(string Name, string? Description)>> getApps)
    {
        _getApps = getApps;
    }

    public override string Name => "launcher";

    public override string? AppDescription => "查看并启动已安装的应用";

    public override ContextWindow CreateWindow(string? intent)
    {
        return new ContextWindow
        {
            Id = "launcher",
            Description = new Text("应用启动器。选择一个应用打开。"),
            Content = new VStack
            {
                Children = BuildAppList()
            },
            Actions =
            [
                new ContextAction
                {
                    Id = "open",
                    Label = "打开应用",
                    Handler = async ctx =>
                    {
                        var appName = ctx.GetString("app");
                        if (string.IsNullOrEmpty(appName))
                        {
                            return ActionResult.Fail("请指定应用名称");
                        }
                        // 返回数据，让调用方处理启动逻辑
                        return ActionResult.Ok(
                            summary: $"打开应用 {appName}",
                            shouldClose: false,
                            data: new { action = "launch", app = appName, close_source = true }
                        );
                    }
                }.WithParam("app", ParamType.String),

                new ContextAction
                {
                    Id = "close",
                    Label = "关闭",
                    Handler = _ => Task.FromResult(ActionResult.Close())
                }
            ]
        };
    }

    private List<IComponent> BuildAppList()
    {
        var apps = _getApps().ToList();
        var components = new List<IComponent>
        {
            new Text("已安装的应用："),
            new Text("")
        };

        for (int i = 0; i < apps.Count; i++)
        {
            var (name, desc) = apps[i];
            var line = $"{i + 1}. {name}";
            if (!string.IsNullOrEmpty(desc))
            {
                line += $" - {desc}";
            }
            components.Add(new Text(line));
        }

        return components;
    }
}
