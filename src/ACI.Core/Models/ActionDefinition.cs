using System.Xml.Linq;

namespace ACI.Core.Models;

/// <summary>
/// 操作定义
/// </summary>
public class ActionDefinition
{
    /// <summary>
    /// 操作 ID
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 操作标签（显示名称）
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Parameter schema (JSON-like).
    /// </summary>
    public ActionParamSchema? ParamsSchema { get; init; }

    /// <summary>
    /// 执行模式（默认同步）
    /// </summary>
    public ActionExecutionMode Mode { get; init; } = ActionExecutionMode.Sync;

    /// <summary>
    /// 渲染为 XML
    /// </summary>
    public XElement ToXml()
    {
        var action = new XElement("action",
            new XAttribute("id", Id),
            Label
        );

        if (ParamsSchema != null)
        {
            action.Add(new XAttribute("params", ParamsSchema.ToPromptSignature()));
        }

        if (Mode == ActionExecutionMode.Async)
        {
            action.Add(new XAttribute("mode", "async"));
        }

        return action;
    }
}

/// <summary>
/// 操作执行模式
/// </summary>
public enum ActionExecutionMode
{
    Sync,
    Async
}

