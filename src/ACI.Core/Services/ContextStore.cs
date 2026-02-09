using ACI.Core.Abstractions;
using ACI.Core.Models;

namespace ACI.Core.Services;

/// <summary>
/// Thread-safe context item storage with active + archive views.
/// </summary>
public class ContextStore
{
    private readonly List<ContextItem> _activeItems = [];
    private readonly List<ContextItem> _archiveItems = [];
    private readonly Dictionary<string, ContextItem> _archiveById = new(StringComparer.Ordinal);
    private readonly ISeqClock _clock;
    private readonly object _lock = new();

    public ContextStore(ISeqClock clock)
    {
        _clock = clock;
    }

    public int CurrentSeq => _clock.CurrentSeq;

    public void Add(ContextItem item)
    {
        lock (_lock)
        {
            item.Seq = _clock.Next();
            item.EstimatedTokens = item.Type == ContextItemType.Window
                ? 0
                : EstimateTokens(item.Content);

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

    public IReadOnlyList<ContextItem> GetAll()
    {
        lock (_lock)
        {
            return _activeItems.OrderBy(i => i.Seq).ToList();
        }
    }

    public IReadOnlyList<ContextItem> GetArchive()
    {
        lock (_lock)
        {
            return _archiveItems.OrderBy(i => i.Seq).ToList();
        }
    }

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

    public void Prune(
        ContextPruner pruner,
        IWindowManager windowManager,
        int maxTokens,
        int minConversationTokens,
        int trimToTokens)
    {
        lock (_lock)
        {
            pruner.Prune(_activeItems, windowManager, maxTokens, minConversationTokens, trimToTokens);
        }
    }

    private static int EstimateTokens(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return 0;
        }

        return (int)Math.Ceiling(content.Length / 2.5);
    }
}
