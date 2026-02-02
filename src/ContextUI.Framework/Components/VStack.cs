using System.Text;
using System.Xml.Linq;

namespace ContextUI.Framework.Components;

/// <summary>
/// 垂直布局组件
/// </summary>
public sealed class VStack : IComponent
{
    /// <summary>
    /// 子元素列表
    /// </summary>
    public List<IComponent> Children { get; init; } = [];

    /// <summary>
    /// 子元素之间的空行数
    /// </summary>
    public int Spacing { get; init; } = 0;

    public XElement ToXml()
    {
        var vstack = new XElement("vstack");
        foreach (var child in Children)
        {
            vstack.Add(child.ToXml());
        }
        return vstack;
    }

    public string Render()
    {
        var separator = Spacing > 0 ? new string('\n', Spacing + 1) : "\n";
        return string.Join(separator, Children.Select(c => c.Render()));
    }
}
