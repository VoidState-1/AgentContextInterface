using ACI.Core.Abstractions;
using ACI.Core.Models;

namespace ACI.Core.Services;

/// <summary>
/// 窗口管理器实现 - 负责窗口生命周期管理
/// </summary>
public class WindowManager : IWindowManager
{
    private readonly Dictionary<string, Window> _windows = [];
    private readonly ISeqClock _clock;

    public WindowManager(ISeqClock clock)
    {
        _clock = clock;
    }

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
    /// 按创建时间排序获取所有窗口
    /// </summary>
    public IEnumerable<Window> GetAllOrdered()
    {
        return _windows.Values.OrderBy(w => w.Meta.CreatedAt);
    }

    /// <summary>
    /// 添加窗口
    /// </summary>
    public void Add(Window window)
    {
        var seq = _clock.CurrentSeq;
        window.Meta.CreatedAt = seq;
        window.Meta.UpdatedAt = seq;

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
    /// 通知窗口更新
    /// </summary>
    public void NotifyUpdated(string id)
    {
        if (_windows.TryGetValue(id, out var window))
        {
            window.Meta.UpdatedAt = _clock.CurrentSeq;
            OnChanged?.Invoke(new WindowChangedEvent(
                WindowEventType.Updated,
                id,
                window
            ));
        }
    }
}
