using ACI.Core.Models;
using ACI.Framework.Components;
using ACI.Framework.Runtime;

namespace ACI.Framework.BuiltIn;

/// <summary>
/// File explorer app for debugging and filesystem inspection.
/// </summary>
public sealed class FileExplorerApp : ContextApp
{
    private const string WindowId = "file_explorer";
    private const string CurrentPathKey = "current_path";
    private const string EntriesKey = "entries";
    private const string DriveModeToken = "__drives__";
    private const int MaxEntries = 160;

    public override string Name => "file_explorer";

    public override string? AppDescription => "Browse real directories, open paths, and navigate parent folders.";

    public override void OnCreate()
    {
        RegisterToolNamespace(Name,
        [
            new ToolDescriptor
            {
                Id = "open_index",
                Params = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["index"] = "integer"
                },
                Description = "Open a directory entry by index."
            },
            new ToolDescriptor
            {
                Id = "open_path",
                Params = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["path"] = "string"
                },
                Description = "Open a directory by absolute path."
            },
            new ToolDescriptor
            {
                Id = "up",
                Description = "Go to parent directory."
            },
            new ToolDescriptor
            {
                Id = "home",
                Description = "Go to current user home directory."
            },
            new ToolDescriptor
            {
                Id = "drives",
                Description = "Switch to drive list."
            },
            new ToolDescriptor
            {
                Id = "refresh",
                Description = "Refresh current listing."
            }
        ]);

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
            new Text($"Current path: {(IsDriveMode(currentPath) ? "[Drive List]" : currentPath)}"),
            new Text($"Entries: {entries.Count} (max shown: {MaxEntries})"),
            new Text(""),
            new Text("Directory entries (index starts from 0):")
        };

        if (entries.Count == 0)
        {
            lines.Add(new Text("[Empty directory]"));
        }
        else
        {
            for (var i = 0; i < entries.Count; i++)
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
                "File explorer. Tools: file_explorer.open_index(index), file_explorer.open_path(path), file_explorer.up, file_explorer.home, file_explorer.drives, file_explorer.refresh."),
            Content = new VStack { Children = lines },
            NamespaceRefs = ["file_explorer", "system"],
            Actions =
            [
                new ContextAction
                {
                    Id = "open_index",
                    Label = "Open By Index",
                    Params = Param.Object(new()
                    {
                        ["index"] = Param.Integer()
                    }),
                    Handler = HandleOpenIndexAsync
                },

                new ContextAction
                {
                    Id = "open_path",
                    Label = "Open Path",
                    Params = Param.Object(new()
                    {
                        ["path"] = Param.String()
                    }),
                    Handler = HandleOpenPathAsync
                },

                new ContextAction
                {
                    Id = "up",
                    Label = "Parent Directory",
                    Handler = HandleUpAsync
                },

                new ContextAction
                {
                    Id = "home",
                    Label = "User Home",
                    Handler = HandleHomeAsync
                },

                new ContextAction
                {
                    Id = "drives",
                    Label = "Drive List",
                    Handler = HandleDrivesAsync
                },

                new ContextAction
                {
                    Id = "refresh",
                    Label = "Refresh",
                    Handler = _ => Task.FromResult(ActionResult.Ok(summary: "Refresh explorer", shouldRefresh: true))
                }
            ]
        };
    }

    private Task<ActionResult> HandleOpenIndexAsync(ACI.Core.Abstractions.ActionContext ctx)
    {
        var index = ctx.GetInt("index");
        if (index == null || index < 0)
        {
            return Task.FromResult(ActionResult.Fail("index is invalid"));
        }

        var entries = State.Get<List<FsEntry>>(EntriesKey) ?? [];
        if (index >= entries.Count)
        {
            return Task.FromResult(ActionResult.Fail($"index out of range, max is {entries.Count - 1}"));
        }

        var selected = entries[index.Value];
        if (!selected.IsDirectory)
        {
            return Task.FromResult(ActionResult.Ok(
                message: $"File: {selected.FullPath}",
                summary: $"Inspect file {selected.Name} ({FormatBytes(selected.SizeBytes)})",
                shouldRefresh: false
            ));
        }

        State.Set(CurrentPathKey, selected.FullPath);
        return Task.FromResult(ActionResult.Ok(
            message: $"Enter directory: {selected.FullPath}",
            summary: $"Enter directory {selected.FullPath}",
            shouldRefresh: true
        ));
    }

    private Task<ActionResult> HandleOpenPathAsync(ACI.Core.Abstractions.ActionContext ctx)
    {
        var path = ctx.GetString("path")?.Trim();
        if (string.IsNullOrEmpty(path))
        {
            return Task.FromResult(ActionResult.Fail("path cannot be empty"));
        }

        if (!Directory.Exists(path))
        {
            return Task.FromResult(ActionResult.Fail($"Directory does not exist: {path}"));
        }

        State.Set(CurrentPathKey, Path.GetFullPath(path));
        return Task.FromResult(ActionResult.Ok(
            message: $"Opened directory: {path}",
            summary: $"Open directory {path}",
            shouldRefresh: true
        ));
    }

    private Task<ActionResult> HandleUpAsync(ACI.Core.Abstractions.ActionContext _)
    {
        var currentPath = State.Get<string>(CurrentPathKey) ?? DriveModeToken;
        if (IsDriveMode(currentPath))
        {
            return Task.FromResult(ActionResult.Ok(message: "Already at drive list", shouldRefresh: true));
        }

        var parent = Directory.GetParent(currentPath)?.FullName;
        if (string.IsNullOrEmpty(parent))
        {
            State.Set(CurrentPathKey, DriveModeToken);
            return Task.FromResult(ActionResult.Ok(
                message: "Back to drive list",
                summary: "Back to drive list",
                shouldRefresh: true
            ));
        }

        State.Set(CurrentPathKey, parent);
        return Task.FromResult(ActionResult.Ok(
            message: $"Back to parent: {parent}",
            summary: $"Back to parent directory {parent}",
            shouldRefresh: true
        ));
    }

    private Task<ActionResult> HandleHomeAsync(ACI.Core.Abstractions.ActionContext _)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!Directory.Exists(home))
        {
            return Task.FromResult(ActionResult.Fail($"User home is unavailable: {home}"));
        }

        State.Set(CurrentPathKey, home);
        return Task.FromResult(ActionResult.Ok(
            message: $"Switched to user home: {home}",
            summary: "Switch to user home",
            shouldRefresh: true
        ));
    }

    private Task<ActionResult> HandleDrivesAsync(ACI.Core.Abstractions.ActionContext _)
    {
        State.Set(CurrentPathKey, DriveModeToken);
        return Task.FromResult(ActionResult.Ok(
            message: "Switched to drive list",
            summary: "View drive list",
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
        public string Name { get; set; } = "";
        public string FullPath { get; set; } = "";
        public bool IsDirectory { get; set; }
        public long SizeBytes { get; set; }
    }
}
