using System.Xml.Linq;
using ContextUI.Core.Abstractions;

namespace ContextUI.Core.Models;

/// <summary>
/// 窗口模型
/// </summary>
public class Window : IRenderable
{
    /// <summary>
    /// 窗口唯一标识
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 窗口内容（可渲染对象）
    /// </summary>
    public required IRenderable Content { get; set; }

    /// <summary>
    /// 可用操作列表
    /// </summary>
    public List<ActionDefinition> Actions { get; init; } = [];

    /// <summary>
    /// 窗口配置
    /// </summary>
    public WindowOptions Options { get; init; } = new();

    /// <summary>
    /// 窗口元信息
    /// </summary>
    public WindowMeta Meta { get; init; } = new();

    /// <summary>
    /// 操作处理器（由 Framework 设置）
    /// </summary>
    public IActionHandler? Handler { get; set; }

    /// <summary>
    /// 渲染为 XML
    /// </summary>
    public XElement ToXml()
    {
        var window = new XElement("Window",
            new XAttribute("id", Id)
        );

        // 内容
        window.Add(new XElement("content", XElement.Parse($"<root>{Content.Render()}</root>").Value));

        // 操作
        var actions = new XElement("actions");
        foreach (var action in Actions)
        {
            actions.Add(action.ToXml());
        }
        window.Add(actions);

        return window;
    }

    public string Render() => ToXml().ToString(SaveOptions.DisableFormatting);
}

/// <summary>
/// 窗口配置选项
/// </summary>
public class WindowOptions
{
    /// <summary>
    /// 是否可关闭
    /// </summary>
    public bool Closable { get; init; } = true;

    /// <summary>
    /// 执行任意操作后是否自动关闭（Popup 场景）
    /// </summary>
    public bool AutoCloseOnAction { get; init; } = false;

    /// <summary>
    /// 窗口标题（可选）
    /// </summary>
    public string? Title { get; init; }
}

/// <summary>
/// 窗口元信息
/// </summary>
public class WindowMeta
{
    /// <summary>
    /// 创建时的序列号
    /// </summary>
    public int CreatedAt { get; set; }

    /// <summary>
    /// 最后更新的序列号
    /// </summary>
    public int UpdatedAt { get; set; }

    /// <summary>
    /// 是否隐藏元信息
    /// </summary>
    public bool Hidden { get; set; } = true;

    /// <summary>
    /// 估算的 token 数量
    /// </summary>
    public int Tokens { get; set; }
}
