using ACI.Core.Abstractions;
using ACI.Core.Models;

namespace ACI.Core.Services;

/// <summary>
/// LLM 消息（简化格式）
/// </summary>
public class LlmMessage
{
    /// <summary>
    /// 角色：system, user, assistant
    /// </summary>
    public required string Role { get; init; }

    /// <summary>
    /// 内容
    /// </summary>
    public required string Content { get; init; }
}

/// <summary>
/// 渲染选项
/// </summary>
public class RenderOptions
{
    /// <summary>
    /// 最大 Token 数
    /// </summary>
    public int MaxTokens { get; set; } = 8000;

    /// <summary>
    /// 对话保护区（对话 Token 低于此值时停止裁剪对话，开始裁剪窗口）
    /// </summary>
    public int MinConversationTokens { get; set; } = 2000;
}

/// <summary>
/// 上下文渲染器接口
/// </summary>
public interface IContextRenderer
{
    /// <summary>
    /// 将上下文项列表渲染为 LLM 消息列表
    /// </summary>
    IReadOnlyList<LlmMessage> Render(
        IReadOnlyList<ContextItem> items,
        IWindowManager windowManager,
        RenderOptions? options = null);
}

/// <summary>
/// 上下文渲染器实现
/// </summary>
public class ContextRenderer : IContextRenderer
{
    /// <summary>
    /// 将上下文项列表渲染为 LLM 消息列表
    /// </summary>
    public IReadOnlyList<LlmMessage> Render(
        IReadOnlyList<ContextItem> items,
        IWindowManager windowManager,
        RenderOptions? options = null)
    {
        options ??= new RenderOptions();

        // 第一遍：渲染所有消息并计算 Token
        var candidates = new List<RenderCandidate>();
        int totalTokens = 0;

        foreach (var item in items)
        {
            var message = RenderItem(item, windowManager);
            if (message == null) continue;

            var tokens = EstimateTokens(message.Content);
            candidates.Add(new RenderCandidate
            {
                Item = item,
                Message = message,
                Tokens = tokens,
                Retained = true
            });
            totalTokens += tokens;
        }

        // 如果不超限，直接返回
        if (totalTokens <= options.MaxTokens)
        {
            return candidates.Select(c => c.Message).ToList();
        }

        // 超限时，执行裁剪策略
        TrimToFit(candidates, ref totalTokens, options);

        // 构建最终消息列表
        return candidates
            .Where(c => c.Retained)
            .Select(c => c.Message)
            .ToList();
    }

    /// <summary>
    /// 裁剪策略：
    /// 1. 先裁剪旧的 User/Assistant 对话
    /// 2. 当对话 Token 低于保护阈值时，开始裁剪旧的 Window
    /// 3. System 永不裁剪
    /// </summary>
    private void TrimToFit(List<RenderCandidate> candidates, ref int totalTokens, RenderOptions options)
    {
        // 计算当前对话 Token
        int conversationTokens = candidates
            .Where(c => c.Retained && (c.Item.Type == ContextItemType.User || c.Item.Type == ContextItemType.Assistant))
            .Sum(c => c.Tokens);

        // 第一阶段：裁剪对话
        for (int i = 0; i < candidates.Count && totalTokens > options.MaxTokens; i++)
        {
            var candidate = candidates[i];

            // 只裁剪对话
            if (candidate.Item.Type != ContextItemType.User && candidate.Item.Type != ContextItemType.Assistant)
            {
                continue;
            }

            // 检查是否达到对话保护阈值
            if (conversationTokens - candidate.Tokens < options.MinConversationTokens)
            {
                break;  // 进入第二阶段
            }

            candidate.Retained = false;
            totalTokens -= candidate.Tokens;
            conversationTokens -= candidate.Tokens;
        }

        // 第二阶段：裁剪窗口（如果还超限）
        for (int i = 0; i < candidates.Count && totalTokens > options.MaxTokens; i++)
        {
            var candidate = candidates[i];

            // 只裁剪窗口
            if (candidate.Item.Type != ContextItemType.Window)
            {
                continue;
            }

            candidate.Retained = false;
            totalTokens -= candidate.Tokens;
        }

        // System 永不裁剪
    }

    /// <summary>
    /// 渲染单个上下文项
    /// </summary>
    private LlmMessage? RenderItem(ContextItem item, IWindowManager windowManager)
    {
        return item.Type switch
        {
            ContextItemType.System => new LlmMessage
            {
                Role = "system",
                Content = item.Content
            },

            ContextItemType.User => new LlmMessage
            {
                Role = "user",
                Content = item.Content
            },

            ContextItemType.Assistant => new LlmMessage
            {
                Role = "assistant",
                Content = item.Content
            },

            ContextItemType.Window => RenderWindowItem(item, windowManager),

            _ => null
        };
    }

    /// <summary>
    /// 渲染窗口项（从 WindowManager 获取最新内容）
    /// </summary>
    private LlmMessage? RenderWindowItem(ContextItem item, IWindowManager windowManager)
    {
        var windowId = item.Content;
        var window = windowManager.Get(windowId);

        if (window == null)
        {
            return null;  // 窗口已关闭
        }

        return new LlmMessage
        {
            Role = "user",  // 窗口状态作为用户观察反馈
            Content = window.Render()
        };
    }

    /// <summary>
    /// 估算 Token 数量
    /// </summary>
    private static int EstimateTokens(string content)
    {
        if (string.IsNullOrEmpty(content)) return 0;

        // 简单估算：中文约 1.5 字符/token，英文约 4 字符/token
        // 取平均值约 2.5 字符/token
        return (int)Math.Ceiling(content.Length / 2.5);
    }

    /// <summary>
    /// 渲染候选项
    /// </summary>
    private class RenderCandidate
    {
        public required ContextItem Item { get; init; }
        public required LlmMessage Message { get; init; }
        public required int Tokens { get; init; }
        public bool Retained { get; set; }
    }
}

