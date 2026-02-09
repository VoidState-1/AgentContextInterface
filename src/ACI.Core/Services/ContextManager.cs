using ACI.Core.Abstractions;
using ACI.Core.Models;

namespace ACI.Core.Services;

/// <summary>
/// IContextManager facade composed from storage and pruning services.
/// </summary>
public class ContextManager : IContextManager
{
    private readonly ContextStore _store;
    private readonly ContextPruner _pruner;

    public ContextManager(ISeqClock clock)
    {
        _store = new ContextStore(clock);
        _pruner = new ContextPruner();
    }

    public int CurrentSeq => _store.CurrentSeq;

    public void Add(ContextItem item) => _store.Add(item);

    public IReadOnlyList<ContextItem> GetAll() => _store.GetAll();

    public IReadOnlyList<ContextItem> GetArchive() => _store.GetArchive();

    public IReadOnlyList<ContextItem> GetActive() => _store.GetActive();

    public ContextItem? GetById(string id) => _store.GetById(id);

    public void MarkWindowObsolete(string windowId) => _store.MarkWindowObsolete(windowId);

    public ContextItem? GetWindowItem(string windowId) => _store.GetWindowItem(windowId);

    public void Prune(
        IWindowManager windowManager,
        int maxTokens,
        int minConversationTokens,
        int pruneTargetTokens)
        => _store.Prune(_pruner, windowManager, maxTokens, minConversationTokens, pruneTargetTokens);
}
