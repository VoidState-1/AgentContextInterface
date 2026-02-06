using System.Xml.Linq;
using ACI.Core.Abstractions;

namespace ACI.Core.Models;

/// <summary>
/// 简单文本内容（用于包装普通字符串）
/// </summary>
public sealed class TextContent : IRenderable
{
    private readonly Func<string> _textFactory;

    /// <summary>
    /// 静态文本
    /// </summary>
    public TextContent(string text)
    {
        _textFactory = () => text;
    }

    /// <summary>
    /// 动态文本（每次渲染时求值）
    /// </summary>
    public TextContent(Func<string> textFactory)
    {
        _textFactory = textFactory;
    }

    public XElement ToXml()
    {
        return new XElement("text", _textFactory());
    }

    public string Render() => _textFactory();
}
