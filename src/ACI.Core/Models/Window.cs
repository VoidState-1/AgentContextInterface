using System.Xml.Linq;
using ACI.Core.Abstractions;

namespace ACI.Core.Models;

/// <summary>
/// 窗口模型。
/// </summary>
public class Window : IRenderable
{
    /// <summary>
    /// 窗口唯一标识。
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 窗口描述。
    /// </summary>
    public IRenderable? Description { get; set; }

    /// <summary>
    /// 窗口主体内容。
    /// </summary>
    public required IRenderable Content { get; set; }

    /// <summary>
    /// 窗口引用的工具命名空间列表。
    /// </summary>
    public List<string> NamespaceRefs { get; init; } = [];

    /// <summary>
    /// 窗口配置。
    /// </summary>
    public WindowOptions Options { get; init; } = new();

    /// <summary>
    /// 窗口元信息。
    /// </summary>
    public WindowMeta Meta { get; init; } = new();

    /// <summary>
    /// 工具执行处理器（由 Framework 注入）。
    /// </summary>
    public IActionHandler? Handler { get; set; }

    /// <summary>
    /// 所属应用名称。
    /// </summary>
    public string? AppName { get; set; }

    /// <summary>
    /// 渲染为 XML。
    /// </summary>
    public XElement ToXml()
    {
        var window = new XElement("Window", new XAttribute("id", Id));

        if (NamespaceRefs.Count > 0)
        {
            window.Add(new XAttribute("ns", string.Join(",", NamespaceRefs)));
        }

        if (Options.RenderMode == RenderMode.Compact)
        {
            window.Add(new XAttribute("compact", true));
            window.Value = Content.Render();
            return window;
        }

        if (!Meta.Hidden)
        {
            var meta = new XElement("meta",
                new XElement("tokens", Meta.Tokens),
                new XElement("created_at", Meta.CreatedAt),
                new XElement("updated_at", Meta.UpdatedAt)
            );
            window.Add(meta);
        }

        if (Description != null)
        {
            window.Add(new XElement("description", Description.Render()));
        }

        window.Add(new XElement("content", Content.Render()));
        return window;
    }

    /// <summary>
    /// 渲染为紧凑字符串。
    /// </summary>
    public string Render() => ToXml().ToString(SaveOptions.DisableFormatting);

    /// <summary>
    /// 渲染为格式化字符串。
    /// </summary>
    public string RenderFormatted() => ToXml().ToString(SaveOptions.None);
}

/// <summary>
/// 窗口配置选项。
/// </summary>
public class WindowOptions
{
    /// <summary>
    /// 是否允许关闭。
    /// </summary>
    public bool Closable { get; init; } = true;

    /// <summary>
    /// 执行动作后是否自动关闭。
    /// </summary>
    public bool AutoCloseOnAction { get; init; } = false;

    /// <summary>
    /// 窗口渲染模式。
    /// </summary>
    public RenderMode RenderMode { get; init; } = RenderMode.Full;

    /// <summary>
    /// 是否在裁剪时固定保留。
    /// </summary>
    public bool PinInPrompt { get; init; } = false;

    /// <summary>
    /// 是否重要窗口。
    /// </summary>
    public bool Important { get; init; } = true;

    /// <summary>
    /// 刷新模式。
    /// </summary>
    public RefreshMode RefreshMode { get; init; } = RefreshMode.InPlace;
}

/// <summary>
/// 渲染模式。
/// </summary>
public enum RenderMode
{
    /// <summary>
    /// 完整模式。
    /// </summary>
    Full,

    /// <summary>
    /// 紧凑模式。
    /// </summary>
    Compact
}

/// <summary>
/// 刷新模式。
/// </summary>
public enum RefreshMode
{
    /// <summary>
    /// 原地刷新。
    /// </summary>
    InPlace,

    /// <summary>
    /// 追加刷新。
    /// </summary>
    Append
}

/// <summary>
/// 窗口元信息。
/// </summary>
public class WindowMeta
{
    /// <summary>
    /// 创建序号。
    /// </summary>
    public int CreatedAt { get; set; }

    /// <summary>
    /// 最后更新序号。
    /// </summary>
    public int UpdatedAt { get; set; }

    /// <summary>
    /// 是否隐藏元信息。
    /// </summary>
    public bool Hidden { get; set; } = true;

    /// <summary>
    /// 估算 token 数。
    /// </summary>
    public int Tokens { get; set; }
}
