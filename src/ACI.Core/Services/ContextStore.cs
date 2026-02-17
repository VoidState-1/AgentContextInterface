using ACI.Core.Abstractions;
using ACI.Core.Models;

namespace ACI.Core.Services;

/// <summary>
/// 线程安全的上下文存储，维护活跃视图与归档视图。
/// </summary>
public class ContextStore
{
    /// <summary>
    /// 上下文数据集合。
    /// </summary>
    private readonly List<ContextItem> _activeItems = [];
    private readonly List<ContextItem> _archiveItems = [];
    private readonly Dictionary<string, ContextItem> _archiveById = new(StringComparer.Ordinal);

    /// <summary>
    /// 基础依赖。
    /// </summary>
    private readonly ISeqClock _clock;

    /// <summary>
    /// 并发访问锁。
    /// </summary>
    private readonly object _lock = new();

    /// <summary>
    /// 创建上下文存储实例。
    /// </summary>
    public ContextStore(ISeqClock clock)
    {
        _clock = clock;
    }

    /// <summary>
    /// 当前时钟序号。
    /// </summary>
    public int CurrentSeq => _clock.CurrentSeq;

    /// <summary>
    /// 添加一条上下文记录并写入归档。
    /// </summary>
    public void Add(ContextItem item)
    {
        lock (_lock)
        {
            // 1. 分配序号并初始化 Token 估算。
            item.Seq = _clock.Next();
            item.EstimatedTokens = item.Type == ContextItemType.Window
                ? 0
                : EstimateTokens(item.Content);

            // 2. 对窗口类条目，先将旧版本标记为过时。
            if (item.Type == ContextItemType.Window)
            {
                foreach (var old in _activeItems.Where(i =>
                             i.Type == ContextItemType.Window &&
                             i.Content == item.Content &&
                             !i.IsObsolete))
                {
                    old.IsObsolete = true;
                }
            }

            // 3. 写入活跃集合与归档集合。
            _activeItems.Add(item);
            _archiveItems.Add(item);
            _archiveById[item.Id] = item;
        }
    }

    /// <summary>
    /// 获取全部条目（包含过时条目）。
    /// </summary>
    public IReadOnlyList<ContextItem> GetAll()
    {
        lock (_lock)
        {
            return _activeItems.OrderBy(i => i.Seq).ToList();
        }
    }

    /// <summary>
    /// 获取归档条目（包含被裁剪条目）。
    /// </summary>
    public IReadOnlyList<ContextItem> GetArchive()
    {
        lock (_lock)
        {
            return _archiveItems.OrderBy(i => i.Seq).ToList();
        }
    }

    /// <summary>
    /// 获取当前活跃条目。
    /// </summary>
    public IReadOnlyList<ContextItem> GetActive()
    {
        lock (_lock)
        {
            return _activeItems
                .Where(i => !i.IsObsolete)
                .OrderBy(i => i.Seq)
                .ToList();
        }
    }

    /// <summary>
    /// 按 ID 获取条目（从归档索引检索）。
    /// </summary>
    public ContextItem? GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        lock (_lock)
        {
            return _archiveById.TryGetValue(id, out var item) ? item : null;
        }
    }

    /// <summary>
    /// 将指定窗口的活跃上下文全部标记为过时。
    /// </summary>
    public void MarkWindowObsolete(string windowId)
    {
        lock (_lock)
        {
            foreach (var item in _activeItems.Where(i =>
                         i.Type == ContextItemType.Window &&
                         i.Content == windowId))
            {
                item.IsObsolete = true;
            }
        }
    }

    /// <summary>
    /// 获取窗口对应的最新活跃条目。
    /// </summary>
    public ContextItem? GetWindowItem(string windowId)
    {
        lock (_lock)
        {
            return _activeItems
                .Where(i => i.Type == ContextItemType.Window &&
                            i.Content == windowId &&
                            !i.IsObsolete)
                .OrderByDescending(i => i.Seq)
                .FirstOrDefault();
        }
    }

    /// <summary>
    /// 对活跃条目执行裁剪。
    /// </summary>
    public void Prune(
        ContextPruner pruner,
        IWindowManager windowManager,
        int maxTokens,
        int minConversationTokens,
        int pruneTargetTokens)
    {
        lock (_lock)
        {
            pruner.Prune(_activeItems, windowManager, maxTokens, minConversationTokens, pruneTargetTokens);
        }
    }

    /// <summary>
    /// 导出活跃条目（用于恢复运行中的上下文视图）。
    /// </summary>
    internal IReadOnlyList<ContextItem> ExportActiveItems()
    {
        lock (_lock)
        {
            return _activeItems.ToList();
        }
    }

    /// <summary>
    /// 导出归档条目（包含被裁剪历史），用于快照备份。
    /// </summary>
    internal IReadOnlyList<ContextItem> ExportArchiveItems()
    {
        lock (_lock)
        {
            return _archiveItems.ToList();
        }
    }

    /// <summary>
    /// 批量导入活跃/归档条目（恢复快照时使用）。
    /// 调用后覆盖现有数据。
    /// 注意：不通过 Add 方法注入，直接设置已有的 Seq/IsObsolete 等元数据。
    /// </summary>
    internal void ImportSnapshotItems(
        IReadOnlyList<ContextItem> activeItems,
        IReadOnlyList<ContextItem> archiveItems)
    {
        lock (_lock)
        {
            _activeItems.Clear();
            _archiveItems.Clear();
            _archiveById.Clear();

            // 1. 先恢复归档（完整历史）。
            foreach (var item in archiveItems.OrderBy(i => i.Seq))
            {
                _archiveItems.Add(item);
                _archiveById[item.Id] = item;
            }

            // 2. 再恢复活跃视图（仅当前参与上下文的条目）。
            foreach (var item in activeItems.OrderBy(i => i.Seq))
            {
                _activeItems.Add(item);

                // 兜底：若活跃条目不在归档中，补入归档索引。
                if (_archiveById.ContainsKey(item.Id))
                {
                    continue;
                }

                _archiveItems.Add(item);
                _archiveById[item.Id] = item;
            }
        }
    }

    /// <summary>
    /// 估算文本对应的 Token 数。
    /// </summary>
    private static int EstimateTokens(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return 0;
        }

        return (int)Math.Ceiling(content.Length / 2.5);
    }
}
