using System.Text;
using System.Xml.Linq;

namespace ACI.Framework.Components;

/// <summary>
/// 树形结构组件
/// </summary>
public sealed class Tree : IComponent
{
    /// <summary>
    /// 根节点列表
    /// </summary>
    public List<TreeNode> Roots { get; init; } = [];

    /// <summary>
    /// 缩进前缀
    /// </summary>
    public string IndentPrefix { get; init; } = "  ";

    public XElement ToXml()
    {
        var tree = new XElement("tree");
        foreach (var root in Roots)
        {
            tree.Add(RenderNodeToXml(root));
        }
        return tree;
    }

    public string Render()
    {
        var sb = new StringBuilder();
        foreach (var root in Roots)
        {
            RenderNode(sb, root, 0);
        }
        return sb.ToString().TrimEnd();
    }

    private XElement RenderNodeToXml(TreeNode node)
    {
        var element = new XElement("node", new XAttribute("label", node.Label));
        foreach (var child in node.Children)
        {
            element.Add(RenderNodeToXml(child));
        }
        return element;
    }

    private void RenderNode(StringBuilder sb, TreeNode node, int depth)
    {
        var indent = string.Concat(Enumerable.Repeat(IndentPrefix, depth));
        var prefix = depth == 0 ? "" : "├─ ";
        sb.AppendLine($"{indent}{prefix}{node.Label}");

        foreach (var child in node.Children)
        {
            RenderNode(sb, child, depth + 1);
        }
    }
}

/// <summary>
/// 树节点
/// </summary>
public class TreeNode
{
    public required string Label { get; init; }
    public List<TreeNode> Children { get; init; } = [];

    public static TreeNode Leaf(string label) => new() { Label = label };

    public static TreeNode Branch(string label, params TreeNode[] children)
        => new() { Label = label, Children = [.. children] };
}
