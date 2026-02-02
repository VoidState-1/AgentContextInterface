using System.Xml.Linq;

namespace ContextUI.Framework.Components;

/// <summary>
/// 水平布局组件
/// </summary>
public sealed class HStack : IComponent
{
    /// <summary>
    /// 子元素列表
    /// </summary>
    public List<IComponent> Children { get; init; } = [];

    /// <summary>
    /// 分隔符
    /// </summary>
    public string Separator { get; init; } = " ";

    public XElement ToXml()
    {
        var hstack = new XElement("hstack");
        foreach (var child in Children)
        {
            hstack.Add(child.ToXml());
        }
        return hstack;
    }

    public string Render()
    {
        return string.Join(Separator, Children.Select(c => c.Render()));
    }
}
