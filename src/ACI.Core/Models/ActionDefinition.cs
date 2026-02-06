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
    /// 参数定义列表
    /// </summary>
    public List<ParameterDefinition> Parameters { get; init; } = [];

    /// <summary>
    /// 渲染为 XML
    /// </summary>
    public XElement ToXml()
    {
        var action = new XElement("action",
            new XAttribute("id", Id),
            Label
        );

        if (Parameters.Count > 0)
        {
            var paramsStr = string.Join(", ",
                Parameters.Select(p => $"{p.Name}:{p.Type}" + (p.Required ? "" : "?")));
            action.Add(new XAttribute("params", paramsStr));
        }

        return action;
    }
}

/// <summary>
/// 参数定义
/// </summary>
public class ParameterDefinition
{
    /// <summary>
    /// 参数名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 参数类型（string, int, bool）
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// 是否必需
    /// </summary>
    public bool Required { get; init; } = true;

    /// <summary>
    /// 默认值
    /// </summary>
    public object? Default { get; init; }
}
