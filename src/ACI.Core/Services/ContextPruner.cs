using ACI.Core.Abstractions;
using ACI.Core.Models;

namespace ACI.Core.Services;

/// <summary>
/// 按 Token 预算裁剪活跃上下文条目。
/// </summary>
public class ContextPruner
{
    /// <summary>
    /// 执行上下文裁剪。
    /// </summary>
    public void Prune(
        List<ContextItem> activeItems,
        IWindowManager windowManager,
        int maxTokens,
        int minConversationTokens,
        int pruneTargetTokens)
    {
        // 1. 参数校验与目标 Token 计算。
        if (windowManager == null)
        {
            throw new ArgumentNullException(nameof(windowManager));
        }

        if (maxTokens <= 0)
        {
            return;
        }

        var targetTokens = pruneTargetTokens <= 0
            ? Math.Max(1, maxTokens / 2)
            : Math.Clamp(pruneTargetTokens, 1, maxTokens);
        var protectedConversationTokens = Math.Clamp(minConversationTokens, 0, targetTokens);

        // 2. 构建候选集并计算当前总量。
        var candidates = BuildPruneCandidates(activeItems, windowManager);
        var totalTokens = candidates.Sum(c => c.Tokens);
        if (totalTokens <= maxTokens)
        {
            return;
        }

        var conversationTokens = candidates
            .Where(c => c.Item.Type is ContextItemType.User or ContextItemType.Assistant)
            .Sum(c => c.Tokens);

        // 3. 第一轮：优先裁剪旧对话，再裁剪非重要窗口。
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

        // 4. 第二轮：在仍超预算时，继续裁剪旧的重要窗口（跳过固定窗口）。
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

    /// <summary>
    /// 生成按时间排序的裁剪候选列表。
    /// </summary>
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

    /// <summary>
    /// 裁剪候选条目。
    /// </summary>
    private class PruneCandidate
    {
        /// <summary>
        /// 原始上下文条目。
        /// </summary>
        public required ContextItem Item { get; init; }

        /// <summary>
        /// 估算 Token 数。
        /// </summary>
        public required int Tokens { get; init; }

        /// <summary>
        /// 是否固定在提示词中不可裁剪。
        /// </summary>
        public bool PinInPrompt { get; init; }

        /// <summary>
        /// 是否重要窗口。
        /// </summary>
        public bool IsImportant { get; init; }
    }
}
