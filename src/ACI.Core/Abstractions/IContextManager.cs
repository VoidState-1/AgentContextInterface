using ACI.Core.Models;

namespace ACI.Core.Abstractions;

/// <summary>
/// 上下文管理器接口 - 管理对话历史和上下文排序
/// </summary>
public interface IContextManager
{
    /// <summary>
    /// 当前序列号（只读）
    /// </summary>
    int CurrentSeq { get; }

    /// <summary>
    /// 添加上下文项（自动分配 Seq）
    /// </summary>
    void Add(ContextItem item);

    /// <summary>
    /// 获取所有上下文项（按 seq 排序）
    /// </summary>
    IReadOnlyList<ContextItem> GetAll();

    /// <summary>
    /// 获取归档备份（按 seq 排序，包含已裁剪条目）
    /// </summary>
    IReadOnlyList<ContextItem> GetArchive();

    /// <summary>
    /// 获取有效的上下文项（排除 IsObsolete，按 seq 排序）
    /// </summary>
    IReadOnlyList<ContextItem> GetActive();

    /// <summary>
    /// 通过 Id 查找上下文项（包含归档）
    /// </summary>
    ContextItem? GetById(string id);

    /// <summary>
    /// 标记窗口相关的上下文项为过时
    /// </summary>
    void MarkWindowObsolete(string windowId);

    /// <summary>
    /// 获取指定窗口的上下文项
    /// </summary>
    ContextItem? GetWindowItem(string windowId);

    /// <summary>
    /// 统一裁剪入口：按 Token 预算真实删除活跃条目
    /// </summary>
    void Prune(
        IWindowManager windowManager,
        int maxTokens,
        int minConversationTokens,
        int pruneTargetTokens);
}

