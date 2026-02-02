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
    /// 获取服务
    /// </summary>
    T? GetService<T>() where T : class;
}
