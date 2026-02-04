namespace ContextUI.Core.Models;

/// <summary>
/// 上下文项类型
/// </summary>
public enum ContextItemType
{
    /// <summary>
    /// 系统提示词
    /// </summary>
    System,

    /// <summary>
    /// 用户消息
    /// </summary>
    User,

    /// <summary>
    /// AI 响应
    /// </summary>
    Assistant,

    /// <summary>
    /// 窗口引用（动态渲染）
    /// </summary>
    Window,

    /// <summary>
    /// 日志/事件
    /// </summary>
    Log
}

/// <summary>
/// 上下文项 - 对话历史中的一个条目
/// </summary>
public class ContextItem
{
    /// <summary>
    /// 唯一标识
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 类型
    /// </summary>
    public required ContextItemType Type { get; init; }

    /// <summary>
    /// 创建时的序列号（决定排序位置）
    /// </summary>
    public required int Seq { get; init; }

    /// <summary>
    /// 内容（对于 Window 类型，这是 WindowId；对于其他类型，是实际内容）
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// 是否已过时（不再包含在上下文中）
    /// </summary>
    public bool IsObsolete { get; set; }

    /// <summary>
    /// 是否粘滞（Token 压缩时优先保留）
    /// </summary>
    public bool IsSticky { get; set; }

    /// <summary>
    /// 估算的 Token 数量（缓存）
    /// </summary>
    public int EstimatedTokens { get; set; }
}
