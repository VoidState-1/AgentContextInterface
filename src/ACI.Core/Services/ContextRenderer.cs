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
    public int MaxTokens { get; set; } = 32000;

    /// <summary>
    /// 对话保护区（对话 Token 低于此值时停止裁剪对话，开始裁剪窗口）
    /// </summary>
    public int MinConversationTokens { get; set; } = 8000;

    /// <summary>
    /// 触发裁剪后收缩到的 Token 目标；小于等于 0 时按 MaxTokens 的一半处理
    /// </summary>
    public int TrimToTokens { get; set; } = 16000;
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
        _ = options;
        var messages = new List<LlmMessage>();

        foreach (var item in items)
        {
            var message = RenderItem(item, windowManager);
            if (message != null)
            {
                messages.Add(message);
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
}

