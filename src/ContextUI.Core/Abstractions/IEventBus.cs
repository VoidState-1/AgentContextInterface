namespace ContextUI.Core.Abstractions;

/// <summary>
/// 事件总线接口 - 统一的事件分发机制
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// 发布事件
    /// </summary>
    void Publish<T>(T evt) where T : IEvent;

    /// <summary>
    /// 订阅事件
    /// </summary>
    IDisposable Subscribe<T>(Action<T> handler) where T : IEvent;
}

/// <summary>
/// 事件标记接口
/// </summary>
public interface IEvent { }
