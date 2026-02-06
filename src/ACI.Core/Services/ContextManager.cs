using ACI.Core.Abstractions;
using ACI.Core.Models;

namespace ACI.Core.Services;

/// <summary>
/// 上下文管理器实现
/// </summary>
public class ContextManager : IContextManager
{
    private readonly List<ContextItem> _items = [];
    private readonly ISeqClock _clock;
    private readonly object _lock = new();

    public ContextManager(ISeqClock clock)
    {
        _clock = clock;
    }

    /// <summary>
    /// 当前序列号
    /// </summary>
    public int CurrentSeq => _clock.CurrentSeq;

    /// <summary>
    /// 添加上下文项（自动分配 Seq）
    /// </summary>
    public void Add(ContextItem item)
    {
        lock (_lock)
        {
            // 自动分配 seq
            item.Seq = _clock.Next();

            // 如果是窗口类型，标记同一窗口的旧项为过时
            if (item.Type == ContextItemType.Window)
            {
                foreach (var old in _items.Where(i =>
                    i.Type == ContextItemType.Window &&
                    i.Content == item.Content &&
                    !i.IsObsolete))
                {
                    old.IsObsolete = true;
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
    /// 清理过旧的上下文项（保留最近的 maxItems 个非窗口项）
    /// </summary>
    public void Prune(int maxItems = 100)
    {
        lock (_lock)
        {
            // 只清理非窗口、非系统的对话项
            var conversationItems = _items
                .Where(i => i.Type == ContextItemType.User || i.Type == ContextItemType.Assistant)
                .OrderBy(i => i.Seq)
                .ToList();

            if (conversationItems.Count > maxItems)
            {
                var toRemove = conversationItems.Take(conversationItems.Count - maxItems).ToList();
                foreach (var item in toRemove)
                {
                    _items.Remove(item);
                }
            }
        }
    }
}

