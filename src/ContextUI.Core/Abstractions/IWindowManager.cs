using ContextUI.Core.Models;

namespace ContextUI.Core.Abstractions;

/// <summary>
/// 窗口管理器接口 - 仅负责窗口生命周期管理
/// </summary>
public interface IWindowManager
{
    /// <summary>
    /// 获取窗口
    /// </summary>
    Window? Get(string id);

    /// <summary>
    /// 获取所有活跃窗口
    /// </summary>
    IEnumerable<Window> GetAll();

    /// <summary>
    /// 添加窗口
    /// </summary>
    void Add(Window window);

    /// <summary>
    /// 移除窗口
    /// </summary>
    void Remove(string id);

    /// <summary>
    /// 窗口变化事件
    /// </summary>
    event Action<WindowChangedEvent>? OnChanged;
}

/// <summary>
/// 窗口变化事件
/// </summary>
public record WindowChangedEvent(
    WindowEventType Type,
    string WindowId,
    Window? Window
);

/// <summary>
/// 窗口事件类型
/// </summary>
public enum WindowEventType
{
    Created,
    Updated,
    Removed
}
