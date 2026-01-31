using System.Xml.Linq;

namespace ContextUI.Core.Abstractions;

/// <summary>
/// 可渲染为 XML 的对象契约
/// </summary>
public interface IRenderable
{
    /// <summary>
    /// 渲染为 XML 元素
    /// </summary>
    XElement ToXml();

    /// <summary>
    /// 渲染为 XML 字符串（单行）
    /// </summary>
    string Render() => ToXml().ToString(SaveOptions.DisableFormatting);

    /// <summary>
    /// 渲染为格式化的 XML 字符串
    /// </summary>
    string RenderFormatted() => ToXml().ToString(SaveOptions.None);
}
