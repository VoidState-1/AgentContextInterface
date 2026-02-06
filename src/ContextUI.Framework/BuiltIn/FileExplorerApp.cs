using ContextUI.Core.Models;
using ContextUI.Framework.Components;
using ContextUI.Framework.Runtime;

namespace ContextUI.Framework.BuiltIn;

/// <summary>
/// 文件浏览器应用（调试用）
/// </summary>
public sealed class FileExplorerApp : ContextApp
{
    private const string WindowId = "file_explorer";
    private const string CurrentPathKey = "current_path";
    private const string EntriesKey = "entries";
    private const string DriveModeToken = "__drives__";
    private const int MaxEntries = 160;

    public override string Name => "file_explorer";

    public override string? AppDescription => "浏览真实文件系统目录，支持目录跳转和上级返回";

    public override void OnCreate()
    {
        if (!State.Has(CurrentPathKey))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            State.Set(CurrentPathKey, Directory.Exists(home) ? home : DriveModeToken);
        }
    }

    public override ContextWindow CreateWindow(string? intent)
    {
        var currentPath = State.Get<string>(CurrentPathKey) ?? DriveModeToken;
        var entries = ListEntries(currentPath);
        State.Set(EntriesKey, entries);

        var lines = new List<IComponent>
        {
            new Text($"当前路径: {(IsDriveMode(currentPath) ? "[Drive List]" : currentPath)}"),
            new Text($"共 {entries.Count} 项（最多显示 {MaxEntries} 项）"),
            new Text(""),
            new Text("目录项（index 从 0 开始）：")
        };

        if (entries.Count == 0)
        {
            lines.Add(new Text("[空目录]"));
        }
        else
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var item = entries[i];
                var prefix = item.IsDirectory ? "[D]" : "[F]";
                var size = item.IsDirectory ? "" : $" ({FormatBytes(item.SizeBytes)})";
                lines.Add(new Text($"{i,3} {prefix} {item.Name}{size}"));
            }
        }

        return new ContextWindow
        {
            Id = WindowId,
            Description = new Text(
                "文件浏览器。可用操作：open_index(index)、open_path(path)、up、home、drives、refresh、close。"),
            Content = new VStack { Children = lines },
            Actions =
            [
                new ContextAction
                {
                    Id = "open_index",
                    Label = "按索引打开",
                    Handler = HandleOpenIndexAsync
                }.WithParam("index", ParamType.Int),

                new ContextAction
                {
                    Id = "open_path",
                    Label = "打开路径",
                    Handler = HandleOpenPathAsync
                }.WithParam("path", ParamType.String),

                new ContextAction
                {
                    Id = "up",
                    Label = "上级目录",
                    Handler = HandleUpAsync
                },

                new ContextAction
                {
                    Id = "home",
                    Label = "用户目录",
                    Handler = HandleHomeAsync
                },

                new ContextAction
                {
                    Id = "drives",
                    Label = "盘符列表",
                    Handler = HandleDrivesAsync
                },

                new ContextAction
                {
                    Id = "refresh",
                    Label = "刷新",
                    Handler = _ => Task.FromResult(ActionResult.Ok(summary: "刷新目录", shouldRefresh: true))
                },

                new ContextAction
                {
                    Id = "close",
                    Label = "关闭",
                    Handler = _ => Task.FromResult(ActionResult.Close("关闭文件浏览器"))
                }
            ]
        };
    }

    private Task<ActionResult> HandleOpenIndexAsync(ContextUI.Core.Abstractions.ActionContext ctx)
    {
        var index = ctx.GetInt("index");
        if (index == null || index < 0)
        {
            return Task.FromResult(ActionResult.Fail("index 参数无效"));
        }

        var entries = State.Get<List<FsEntry>>(EntriesKey) ?? [];
        if (index >= entries.Count)
        {
            return Task.FromResult(ActionResult.Fail($"index 超出范围，当前最大为 {entries.Count - 1}"));
        }

        var selected = entries[index.Value];
        if (!selected.IsDirectory)
        {
            return Task.FromResult(ActionResult.Ok(
                message: $"文件: {selected.FullPath}",
                summary: $"查看文件 {selected.Name} ({FormatBytes(selected.SizeBytes)})",
                shouldRefresh: false
            ));
        }

        State.Set(CurrentPathKey, selected.FullPath);
        return Task.FromResult(ActionResult.Ok(
            message: $"已进入目录: {selected.FullPath}",
            summary: $"进入目录 {selected.FullPath}",
            shouldRefresh: true
        ));
    }

    private Task<ActionResult> HandleOpenPathAsync(ContextUI.Core.Abstractions.ActionContext ctx)
    {
        var path = ctx.GetString("path")?.Trim();
        if (string.IsNullOrEmpty(path))
        {
            return Task.FromResult(ActionResult.Fail("path 参数不能为空"));
        }

        if (!Directory.Exists(path))
        {
            return Task.FromResult(ActionResult.Fail($"目录不存在: {path}"));
        }

        State.Set(CurrentPathKey, Path.GetFullPath(path));
        return Task.FromResult(ActionResult.Ok(
            message: $"已打开目录: {path}",
            summary: $"打开目录 {path}",
            shouldRefresh: true
        ));
    }

    private Task<ActionResult> HandleUpAsync(ContextUI.Core.Abstractions.ActionContext _)
    {
        var currentPath = State.Get<string>(CurrentPathKey) ?? DriveModeToken;
        if (IsDriveMode(currentPath))
        {
            return Task.FromResult(ActionResult.Ok(message: "当前已经是盘符列表", shouldRefresh: true));
        }

        var parent = Directory.GetParent(currentPath)?.FullName;
        if (string.IsNullOrEmpty(parent))
        {
            State.Set(CurrentPathKey, DriveModeToken);
            return Task.FromResult(ActionResult.Ok(
                message: "已返回盘符列表",
                summary: "返回盘符列表",
                shouldRefresh: true
            ));
        }

        State.Set(CurrentPathKey, parent);
        return Task.FromResult(ActionResult.Ok(
            message: $"已返回上级: {parent}",
            summary: $"返回上级目录 {parent}",
            shouldRefresh: true
        ));
    }

    private Task<ActionResult> HandleHomeAsync(ContextUI.Core.Abstractions.ActionContext _)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!Directory.Exists(home))
        {
            return Task.FromResult(ActionResult.Fail($"用户目录不可用: {home}"));
        }

        State.Set(CurrentPathKey, home);
        return Task.FromResult(ActionResult.Ok(
            message: $"已切换到用户目录: {home}",
            summary: "切换到用户目录",
            shouldRefresh: true
        ));
    }

    private Task<ActionResult> HandleDrivesAsync(ContextUI.Core.Abstractions.ActionContext _)
    {
        State.Set(CurrentPathKey, DriveModeToken);
        return Task.FromResult(ActionResult.Ok(
            message: "已切换到盘符列表",
            summary: "查看盘符列表",
            shouldRefresh: true
        ));
    }

    private static List<FsEntry> ListEntries(string currentPath)
    {
        try
        {
            if (IsDriveMode(currentPath))
            {
                return DriveInfo.GetDrives()
                    .Where(d => d.IsReady)
                    .Select(d => new FsEntry
                    {
                        Name = d.Name,
                        FullPath = d.RootDirectory.FullName,
                        IsDirectory = true,
                        SizeBytes = 0
                    })
                    .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                    .Take(MaxEntries)
                    .ToList();
            }

            var directories = Directory.GetDirectories(currentPath)
                .Select(path => new FsEntry
                {
                    Name = Path.GetFileName(path),
                    FullPath = path,
                    IsDirectory = true
                });

            var files = Directory.GetFiles(currentPath)
                .Select(path =>
                {
                    var info = new FileInfo(path);
                    return new FsEntry
                    {
                        Name = Path.GetFileName(path),
                        FullPath = path,
                        IsDirectory = false,
                        SizeBytes = info.Exists ? info.Length : 0
                    };
                });

            return directories
                .Concat(files)
                .OrderByDescending(e => e.IsDirectory)
                .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .Take(MaxEntries)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static bool IsDriveMode(string path)
        => string.Equals(path, DriveModeToken, StringComparison.Ordinal);

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / 1024.0 / 1024.0:F1} MB";
        return $"{bytes / 1024.0 / 1024.0 / 1024.0:F1} GB";
    }

    private sealed class FsEntry
    {
        public required string Name { get; init; }
        public required string FullPath { get; init; }
        public required bool IsDirectory { get; init; }
        public long SizeBytes { get; init; }
    }
}
