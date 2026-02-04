using ContextUI.Core.Abstractions;
using ContextUI.Core.Models;

namespace ContextUI.Core.Services;

/// <summary>
/// 上下文管理器实现
/// </summary>
public class ContextManager : IContextManager
{
    private readonly List<ContextItem> _items = [];
    private readonly object _lock = new();

    /// <summary>
    /// 添加上下文项
    /// </summary>
    public void Add(ContextItem item)
    {
        lock (_lock)
        {
            // 如果是窗口类型，标记同一窗口的旧项为过时
            if (item.Type == ContextItemType.Window)
            {
                foreach (var old in _items.Where(i =>
                    i.Type == ContextItemType.Window &&
                    i.Content == item.Content &&
                    !i.IsObsolete))
                {
                    old.IsObsolete = true;
                    old.IsSticky = false;
                }
            }

            _items.Add(item);
        }
    }

    /// <summary>
    /// 获取所有上下文项（按 seq 排序）
    /// </summary>
    public IReadOnlyList<ContextItem> GetAll()
    {
        lock (_lock)
        {
            return _items.OrderBy(i => i.Seq).ToList();
        }
    }

    /// <summary>
    /// 获取有效的上下文项（排除 IsObsolete，按 seq 排序）
    /// </summary>
    public IReadOnlyList<ContextItem> GetActive()
    {
        lock (_lock)
        {
            return _items
                .Where(i => !i.IsObsolete)
                .OrderBy(i => i.Seq)
                .ToList();
        }
    }

    /// <summary>
    /// 标记窗口相关的上下文项为过时
    /// </summary>
    public void MarkWindowObsolete(string windowId)
    {
        lock (_lock)
        {
            foreach (var item in _items.Where(i =>
                i.Type == ContextItemType.Window &&
                i.Content == windowId))
            {
                item.IsObsolete = true;
                item.IsSticky = false;
            }
        }
    }

    /// <summary>
    /// 获取指定窗口的上下文项（最新的未过时项）
    /// </summary>
    public ContextItem? GetWindowItem(string windowId)
    {
        lock (_lock)
        {
            return _items
                .Where(i => i.Type == ContextItemType.Window &&
                           i.Content == windowId &&
                           !i.IsObsolete)
                .OrderByDescending(i => i.Seq)
                .FirstOrDefault();
        }
    }

    /// <summary>
    /// 清理过时的非粘滞日志项
    /// </summary>
    public void Prune(int maxItems = 100)
    {
        lock (_lock)
        {
            var logItems = _items
                .Where(i => i.Type == ContextItemType.Log && !i.IsSticky)
                .OrderBy(i => i.Seq)
                .ToList();

            if (logItems.Count > maxItems)
            {
                var toRemove = logItems.Take(logItems.Count - maxItems).ToList();
                foreach (var item in toRemove)
                {
                    _items.Remove(item);
                }
            }
        }
    }
}
