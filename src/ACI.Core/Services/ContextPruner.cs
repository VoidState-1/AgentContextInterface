using ACI.Core.Abstractions;
using ACI.Core.Models;

namespace ACI.Core.Services;

/// <summary>
/// Applies token-budget pruning policy to active context items.
/// </summary>
public class ContextPruner
{
    public void Prune(
        List<ContextItem> activeItems,
        IWindowManager windowManager,
        int maxTokens,
        int minConversationTokens,
        int trimToTokens)
    {
        if (windowManager == null)
        {
            throw new ArgumentNullException(nameof(windowManager));
        }

        if (maxTokens <= 0)
        {
            return;
        }

        var targetTokens = trimToTokens <= 0
            ? Math.Max(1, maxTokens / 2)
            : Math.Clamp(trimToTokens, 1, maxTokens);
        var protectedConversationTokens = Math.Clamp(minConversationTokens, 0, targetTokens);

        var candidates = BuildPruneCandidates(activeItems, windowManager);
        var totalTokens = candidates.Sum(c => c.Tokens);
        if (totalTokens <= maxTokens)
        {
            return;
        }

        var conversationTokens = candidates
            .Where(c => c.Item.Type is ContextItemType.User or ContextItemType.Assistant)
            .Sum(c => c.Tokens);

        // Phase 1: prune older User/Assistant first, and non-important windows early.
        for (var i = 0; i < candidates.Count && totalTokens > targetTokens; i++)
        {
            var candidate = candidates[i];
            if (candidate.Item.Type is ContextItemType.User or ContextItemType.Assistant)
            {
                if (conversationTokens - candidate.Tokens < protectedConversationTokens)
                {
                    continue;
                }

                if (activeItems.Remove(candidate.Item))
                {
                    totalTokens -= candidate.Tokens;
                    conversationTokens -= candidate.Tokens;
                }

                continue;
            }

            if (candidate.Item.Type == ContextItemType.Window &&
                !candidate.IsImportant &&
                !candidate.PinInPrompt &&
                activeItems.Remove(candidate.Item))
            {
                totalTokens -= candidate.Tokens;
            }
        }

        // Phase 2: prune older important windows (except pinned windows).
        for (var i = 0; i < candidates.Count && totalTokens > targetTokens; i++)
        {
            var candidate = candidates[i];
            if (candidate.Item.Type != ContextItemType.Window)
            {
                continue;
            }

            if (candidate.PinInPrompt || !candidate.IsImportant)
            {
                continue;
            }

            if (activeItems.Remove(candidate.Item))
            {
                totalTokens -= candidate.Tokens;
            }
        }
    }

    private static List<PruneCandidate> BuildPruneCandidates(
        List<ContextItem> activeItems,
        IWindowManager windowManager)
    {
        return activeItems
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
                        PinInPrompt = window?.Options.PinInPrompt == true,
                        IsImportant = window?.Options.Important ?? true
                    };
                }

                var contentTokens = EstimateTokens(item.Content);
                item.EstimatedTokens = contentTokens;
                return new PruneCandidate
                {
                    Item = item,
                    Tokens = contentTokens,
                    PinInPrompt = false,
                    IsImportant = true
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
        public bool IsImportant { get; init; }
    }
}
