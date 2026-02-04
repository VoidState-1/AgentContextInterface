using ContextUI.Core.Models;

namespace ContextUI.Core.Abstractions;

/// <summary>
/// 上下文管理器接口 - 管理对话历史和上下文排序
/// </summary>
public interface IContextManager
{
    /// <summary>
    /// 添加上下文项
    /// </summary>
    void Add(ContextItem item);

    /// <summary>
    /// 获取所有上下文项（按 seq 排序）
    /// </summary>
    IReadOnlyList<ContextItem> GetAll();

    /// <summary>
    /// 获取有效的上下文项（排除 IsObsolete）
    /// </summary>
    IReadOnlyList<ContextItem> GetActive();

    /// <summary>
    /// 标记窗口相关的上下文项为过时
    /// </summary>
    void MarkWindowObsolete(string windowId);

    /// <summary>
    /// 获取指定窗口的上下文项
    /// </summary>
    ContextItem? GetWindowItem(string windowId);

    /// <summary>
    /// 清理过时的非粘滞项
    /// </summary>
    void Prune(int maxItems = 100);
}
