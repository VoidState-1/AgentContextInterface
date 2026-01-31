using ContextUI.Core.Abstractions;
using ContextUI.Core.Models;

namespace ContextUI.Core.Services;

/// <summary>
/// 窗口管理器实现 - 仅负责窗口生命周期
/// </summary>
public class WindowManager : IWindowManager
{
    private readonly Dictionary<string, Window> _windows = [];

    /// <summary>
    /// 窗口变化事件
    /// </summary>
    public event Action<WindowChangedEvent>? OnChanged;

    /// <summary>
    /// 获取窗口
    /// </summary>
    public Window? Get(string id)
    {
        return _windows.GetValueOrDefault(id);
    }

    /// <summary>
    /// 获取所有活跃窗口
    /// </summary>
    public IEnumerable<Window> GetAll()
    {
        return _windows.Values;
    }

    /// <summary>
    /// 添加窗口
    /// </summary>
    public void Add(Window window)
    {
        _windows[window.Id] = window;
        OnChanged?.Invoke(new WindowChangedEvent(
            WindowEventType.Created,
            window.Id,
            window
        ));
    }

    /// <summary>
    /// 移除窗口
    /// </summary>
    public void Remove(string id)
    {
        if (_windows.TryGetValue(id, out var window))
        {
            _windows.Remove(id);
            OnChanged?.Invoke(new WindowChangedEvent(
                WindowEventType.Removed,
                id,
                window
            ));
        }
    }

    /// <summary>
    /// 触发窗口更新事件
    /// </summary>
    public void NotifyUpdated(string id)
    {
        if (_windows.TryGetValue(id, out var window))
        {
            OnChanged?.Invoke(new WindowChangedEvent(
                WindowEventType.Updated,
                id,
                window
            ));
        }
    }
}
