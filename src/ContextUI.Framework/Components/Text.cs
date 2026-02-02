using System.Xml.Linq;
using ContextUI.Core.Abstractions;

namespace ContextUI.Framework.Components;

/// <summary>
/// 组件接口（继承 IRenderable）
/// </summary>
public interface IComponent : IRenderable
{
}

/// <summary>
/// 文本组件
/// </summary>
public sealed class Text : IComponent
{
    private readonly Func<string> _textFactory;

    /// <summary>
    /// 静态文本
    /// </summary>
    public Text(string text)
    {
        _textFactory = () => text;
    }

    /// <summary>
    /// 动态文本（每次渲染时求值）
    /// </summary>
    public Text(Func<string> textFactory)
    {
        _textFactory = textFactory;
    }

    public XElement ToXml() => new("text", _textFactory());

    public string Render() => _textFactory();
}
