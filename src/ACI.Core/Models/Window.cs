using System.Xml.Linq;
using ACI.Core.Abstractions;

namespace ACI.Core.Models;

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
    /// 窗口描述（告诉 AI 这是什么、怎么操作，类似 prompt）
    /// </summary>
    public IRenderable? Description { get; set; }

    /// <summary>
    /// 窗口主内容
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
    /// 所属应用名称
    /// </summary>
    public string? AppName { get; set; }

    /// <summary>
    /// 渲染为 XML
    /// </summary>
    public XElement ToXml()
    {
        var window = new XElement("Window",
            new XAttribute("id", Id)
        );

        // 精简模式：只输出内容
        if (Options.RenderMode == RenderMode.Compact)
        {
            window.Add(new XAttribute("compact", true));
            window.Value = Content.Render();
            return window;
        }

        // 完整模式

        // Meta（如果不隐藏）
        if (!Meta.Hidden)
        {
            var meta = new XElement("meta",
                new XElement("tokens", Meta.Tokens),
                new XElement("created_at", Meta.CreatedAt),
                new XElement("updated_at", Meta.UpdatedAt)
            );
            window.Add(meta);
        }

        // Description
        if (Description != null)
        {
            window.Add(new XElement("description", Description.Render()));
        }

        // Content
        window.Add(new XElement("content", Content.Render()));

        // Actions
        if (Actions.Count > 0)
        {
            var actions = new XElement("actions");
            foreach (var action in Actions)
            {
                actions.Add(action.ToXml());
            }
            window.Add(actions);
        }

        return window;
    }

    public string Render() => ToXml().ToString(SaveOptions.DisableFormatting);

    public string RenderFormatted() => ToXml().ToString(SaveOptions.None);
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
    /// 执行任意操作后是否自动关闭
    /// </summary>
    public bool AutoCloseOnAction { get; init; } = false;

    /// <summary>
    /// 渲染模式
    /// </summary>
    public RenderMode RenderMode { get; init; } = RenderMode.Full;

    /// <summary>
    /// 是否在渲染裁剪时始终保留
    /// </summary>
    public bool PinInPrompt { get; init; } = false;

    /// <summary>
    /// 是否重要窗口（默认重要）。上下文裁剪会优先移除非重要窗口。
    /// </summary>
    public bool Important { get; init; } = true;

    /// <summary>
    /// 刷新模式
    /// </summary>
    public RefreshMode RefreshMode { get; init; } = RefreshMode.InPlace;
}

/// <summary>
/// 渲染模式
/// </summary>
public enum RenderMode
{
    /// <summary>
    /// 完整模式：包含 meta, description, content, actions
    /// </summary>
    Full,

    /// <summary>
    /// 精简模式：只有内容，无包装
    /// </summary>
    Compact
}

/// <summary>
/// 刷新模式
/// </summary>
public enum RefreshMode
{
    /// <summary>
    /// 原地刷新（更新现有上下文消息）
    /// </summary>
    InPlace,

    /// <summary>
    /// 追加刷新（添加新消息到末尾）
    /// </summary>
    Append
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
