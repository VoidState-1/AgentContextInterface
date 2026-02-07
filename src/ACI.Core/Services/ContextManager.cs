using ACI.Core.Abstractions;
using ACI.Core.Models;

namespace ACI.Core.Services;

/// <summary>
/// 上下文管理器实现
/// </summary>
public class ContextManager : IContextManager
{
    private readonly List<ContextItem> _activeItems = [];
    private readonly List<ContextItem> _archiveItems = [];
    private readonly Dictionary<string, ContextItem> _archiveById = new(StringComparer.Ordinal);
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
            item.EstimatedTokens = item.Type == ContextItemType.Window
                ? 0
                : EstimateTokens(item.Content);

            // 如果是窗口类型，标记同一窗口的旧项为过时
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

            _activeItems.Add(item);
            _archiveItems.Add(item);
            _archiveById[item.Id] = item;
        }
    }

    /// <summary>
    /// 获取所有上下文项（按 seq 排序）
    /// </summary>
    public IReadOnlyList<ContextItem> GetAll()
    {
        lock (_lock)
        {
            return _activeItems.OrderBy(i => i.Seq).ToList();
        }
    }

    /// <summary>
    /// 获取归档备份（按 seq 排序）
    /// </summary>
    public IReadOnlyList<ContextItem> GetArchive()
    {
        lock (_lock)
        {
            return _archiveItems.OrderBy(i => i.Seq).ToList();
        }
    }

    /// <summary>
    /// 获取有效的上下文项（排除 IsObsolete，按 seq 排序）
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
    /// 通过 Id 查找上下文项（包含归档）
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
    /// 标记窗口相关的上下文项为过时
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
    /// 获取指定窗口的上下文项（最新的未过时项）
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
    /// 统一裁剪入口：按 Token 预算真实删除活跃条目
    /// </summary>
    public void Prune(
        IWindowManager windowManager,
        int maxTokens,
        int minConversationTokens,
        int trimToTokens)
    {
        if (windowManager == null)
        {
            throw new ArgumentNullException(nameof(windowManager));
        }

        lock (_lock)
        {
            if (maxTokens <= 0)
            {
                return;
            }

            var targetTokens = trimToTokens <= 0
                ? Math.Max(1, maxTokens / 2)
                : Math.Clamp(trimToTokens, 1, maxTokens);
            var protectedConversationTokens = Math.Clamp(minConversationTokens, 0, targetTokens);

            var candidates = BuildPruneCandidates(windowManager);
            var totalTokens = candidates.Sum(c => c.Tokens);
            if (totalTokens <= maxTokens)
            {
                return;
            }

            var conversationTokens = candidates
                .Where(c => c.Item.Type == ContextItemType.User || c.Item.Type == ContextItemType.Assistant)
                .Sum(c => c.Tokens);

            // 第一阶段：裁剪旧的 User / Assistant
            for (var i = 0; i < candidates.Count && totalTokens > targetTokens; i++)
            {
                var candidate = candidates[i];
                if (candidate.Item.Type != ContextItemType.User &&
                    candidate.Item.Type != ContextItemType.Assistant)
                {
                    continue;
                }

                if (conversationTokens - candidate.Tokens < protectedConversationTokens)
                {
                    continue;
                }

                if (_activeItems.Remove(candidate.Item))
                {
                    totalTokens -= candidate.Tokens;
                    conversationTokens -= candidate.Tokens;
                }
            }

            // 第二阶段：裁剪旧的 Window（PinInPrompt 不裁）
            for (var i = 0; i < candidates.Count && totalTokens > targetTokens; i++)
            {
                var candidate = candidates[i];
                if (candidate.Item.Type != ContextItemType.Window)
                {
                    continue;
                }

                if (candidate.PinInPrompt)
                {
                    continue;
                }

                if (_activeItems.Remove(candidate.Item))
                {
                    totalTokens -= candidate.Tokens;
                }
            }
        }
    }

    private List<PruneCandidate> BuildPruneCandidates(IWindowManager windowManager)
    {
        return _activeItems
            .Where(i => !i.IsObsolete)
            .OrderBy(i => i.Seq)
            .Select(item =>
            {
                if (item.Type == ContextItemType.Window)
                {
                    var window = windowManager.Get(item.Content);
                    var windowContent = window?.Render() ?? string.Empty;
                    var windowTokens = EstimateTokens(windowContent);

                    item.EstimatedTokens = windowTokens;
                    return new PruneCandidate
                    {
                        Item = item,
                        Tokens = windowTokens,
                        PinInPrompt = window?.Options.PinInPrompt == true
                    };
                }

                var contentTokens = EstimateTokens(item.Content);
                item.EstimatedTokens = contentTokens;
                return new PruneCandidate
                {
                    Item = item,
                    Tokens = contentTokens,
                    PinInPrompt = false
                };
            })
            .ToList();
    }

    private static int EstimateTokens(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return 0;
        }

        return (int)Math.Ceiling(content.Length / 2.5);
    }

    private class PruneCandidate
    {
        public required ContextItem Item { get; init; }
        public required int Tokens { get; init; }
        public bool PinInPrompt { get; init; }
    }
}

