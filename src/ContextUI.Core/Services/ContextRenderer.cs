using ContextUI.Core.Abstractions;
using ContextUI.Core.Models;

namespace ContextUI.Core.Services;

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
/// 上下文渲染器接口
/// </summary>
public interface IContextRenderer
{
    /// <summary>
    /// 将上下文项列表渲染为 LLM 消息列表
    /// </summary>
    /// <param name="items">上下文项列表（已排序）</param>
    /// <param name="windowManager">窗口管理器（用于获取窗口最新内容）</param>
    /// <param name="maxTokens">最大 Token 数</param>
    IReadOnlyList<LlmMessage> Render(
        IReadOnlyList<ContextItem> items,
        IWindowManager windowManager,
        int maxTokens = 8000);
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
        int maxTokens = 8000)
    {
        var messages = new List<LlmMessage>();
        int totalTokens = 0;

        // 第一遍：计算所有消息并估算 Token
        var candidates = new List<(ContextItem Item, LlmMessage Message, int Tokens)>();

        foreach (var item in items)
        {
            var message = RenderItem(item, windowManager);
            if (message == null) continue;

            var tokens = EstimateTokens(message.Content);
            candidates.Add((item, message, tokens));
            totalTokens += tokens;
        }

        // 如果不超限，直接返回
        if (totalTokens <= maxTokens)
        {
            return candidates.Select(c => c.Message).ToList();
        }

        // 超限时，从最早的非粘滞项开始裁剪
        var retained = new bool[candidates.Count];
        for (int i = 0; i < candidates.Count; i++)
        {
            retained[i] = true;
        }

        for (int i = 0; i < candidates.Count && totalTokens > maxTokens; i++)
        {
            var (item, _, tokens) = candidates[i];

            // 粘滞项和系统消息不裁剪
            if (item.IsSticky || item.Type == ContextItemType.System)
            {
                continue;
            }

            retained[i] = false;
            totalTokens -= tokens;
        }

        // 构建最终消息列表
        for (int i = 0; i < candidates.Count; i++)
        {
            if (retained[i])
            {
                messages.Add(candidates[i].Message);
            }
        }

        return messages;
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

            ContextItemType.Log => new LlmMessage
            {
                Role = "user",  // 日志作为用户观察反馈
                Content = item.Content
            },

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
}
