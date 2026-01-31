using System.Xml.Linq;
using ContextUI.Core.Abstractions;

namespace ContextUI.Core.Models;

/// <summary>
/// 日志条目抽象基类
/// </summary>
public abstract class LogEntry : IRenderable
{
    /// <summary>
    /// 日志序列号（逻辑时钟，全局唯一递增）
    /// </summary>
    public int Seq { get; set; }

    /// <summary>
    /// 当前日志级别（用于分级显示，1=最详细，4=最简略）
    /// </summary>
    public int Level { get; set; } = 1;

    /// <summary>
    /// 是否为永久保留日志（不会被缓冲区清理）
    /// </summary>
    public bool IsPersistent { get; set; }

    /// <summary>
    /// 渲染为 XML 元素（由子类实现）
    /// </summary>
    public abstract XElement ToXml();

    /// <summary>
    /// 渲染为 XML 字符串
    /// </summary>
    public string Render() => ToXml().ToString(SaveOptions.DisableFormatting);
}

/// <summary>
/// 创建应用日志
/// </summary>
public sealed class CreateLogEntry : LogEntry
{
    public required string AppName { get; init; }
    public string? Target { get; init; }
    public required bool Success { get; init; }

    public override XElement ToXml()
    {
        var log = new XElement("Log",
            new XAttribute("seq", Seq),
            new XAttribute("type", "create"),
            new XElement("app", AppName),
            new XElement("result", Success ? "success" : "failure")
        );

        if (!string.IsNullOrWhiteSpace(Target))
        {
            log.Add(new XElement("target", Target));
        }

        return log;
    }
}

/// <summary>
/// 操作执行日志
/// </summary>
public sealed class ActionLogEntry : LogEntry
{
    public required string WindowId { get; init; }
    public required string ActionId { get; init; }
    public required bool Success { get; init; }
    public string? Summary { get; init; }
    public bool Auto { get; init; }

    public override XElement ToXml()
    {
        var log = new XElement("Log",
            new XAttribute("seq", Seq),
            new XAttribute("type", "action"),
            new XElement("window", WindowId),
            new XElement("action", ActionId),
            new XElement("result", Success ? "success" : "failure")
        );

        if (Auto)
        {
            log.Add(new XAttribute("auto", "true"));
        }

        if (!string.IsNullOrWhiteSpace(Summary))
        {
            log.Add(new XElement("summary", Summary));
        }

        return log;
    }
}
