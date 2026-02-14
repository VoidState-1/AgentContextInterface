using ACI.Core.Abstractions;
using ACI.Core.Models;

namespace ACI.Core.Services;

/// <summary>
/// `IContextManager` 外观实现，组合存储与裁剪服务。
/// </summary>
public class ContextManager : IContextManager
{
    /// <summary>
    /// 上下文存储服务。
    /// </summary>
    private readonly ContextStore _store;

    /// <summary>
    /// 暴露内部存储（供 ACI.Server 快照采集/恢复使用）。
    /// </summary>
    internal ContextStore Store => _store;

    /// <summary>
    /// 上下文裁剪服务。
    /// </summary>
    private readonly ContextPruner _pruner;

    /// <summary>
    /// 创建上下文管理器。
    /// </summary>
    public ContextManager(ISeqClock clock)
    {
        _store = new ContextStore(clock);
        _pruner = new ContextPruner();
    }

    /// <summary>
    /// 获取当前序列号。
    /// </summary>
    public int CurrentSeq => _store.CurrentSeq;

    /// <summary>
    /// 添加上下文条目。
    /// </summary>
    public void Add(ContextItem item) => _store.Add(item);

    /// <summary>
    /// 获取全部上下文条目。
    /// </summary>
    public IReadOnlyList<ContextItem> GetAll() => _store.GetAll();

    /// <summary>
    /// 获取归档条目。
    /// </summary>
    public IReadOnlyList<ContextItem> GetArchive() => _store.GetArchive();

    /// <summary>
    /// 获取活跃条目。
    /// </summary>
    public IReadOnlyList<ContextItem> GetActive() => _store.GetActive();

    /// <summary>
    /// 按 ID 获取条目。
    /// </summary>
    public ContextItem? GetById(string id) => _store.GetById(id);

    /// <summary>
    /// 标记窗口条目为过时。
    /// </summary>
    public void MarkWindowObsolete(string windowId) => _store.MarkWindowObsolete(windowId);

    /// <summary>
    /// 获取窗口当前条目。
    /// </summary>
    public ContextItem? GetWindowItem(string windowId) => _store.GetWindowItem(windowId);

    /// <summary>
    /// 执行上下文裁剪。
    /// </summary>
    public void Prune(
        IWindowManager windowManager,
        int maxTokens,
        int minConversationTokens,
        int pruneTargetTokens)
        => _store.Prune(_pruner, windowManager, maxTokens, minConversationTokens, pruneTargetTokens);
}
