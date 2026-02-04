using ContextUI.Core.Abstractions;
using ContextUI.Core.Models;

namespace ContextUI.Framework.Runtime;

/// <summary>
/// 应用运行时上下文接口
/// </summary>
public interface IContext
{
    /// <summary>
    /// 窗口管理
    /// </summary>
    IWindowManager Windows { get; }

    /// <summary>
    /// 事件发布
    /// </summary>
    IEventBus Events { get; }

    /// <summary>
    /// 时钟（获取 seq）
    /// </summary>
    ISeqClock Clock { get; }

    /// <summary>
    /// 上下文管理（对话历史）
    /// </summary>
    IContextManager Context { get; }

    /// <summary>
    /// 请求刷新窗口（通知框架重新渲染窗口内容）
    /// </summary>
    void RequestRefresh(string windowId);

    /// <summary>
    /// 获取服务
    /// </summary>
    T? GetService<T>() where T : class;
}
