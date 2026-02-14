using System.Diagnostics;
using ACI.Framework.Runtime;
using ACI.LLM;
using ACI.LLM.Abstractions;
using ACI.Storage;
using ACI.Server.Settings;

namespace ACI.Server.Services;

/// <summary>
/// Multi-agent session container.
/// </summary>
public class Session : IDisposable
{
    public string SessionId { get; }
    public DateTime CreatedAt { get; }

    private readonly Dictionary<string, AgentContext> _agents = [];
    private readonly object _sync = new();
    private readonly Queue<AgentWakeup> _wakeupQueue = new();
    private readonly HashSet<string> _queuedAgentIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Queue<ChannelMessage>> _pendingMessagesByAgent =
        new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    /// <summary>
    /// Guard against infinite wakeup loops.
    /// </summary>
    private const int MaxWakeupLoopDepth = 20;

    public Session(
        string sessionId,
        IReadOnlyList<AgentProfile> agentProfiles,
        ILLMBridge llmBridge,
        ACIOptions options,
        Action<FrameworkHost>? configureApps = null)
    {
        SessionId = sessionId;
        CreatedAt = DateTime.UtcNow;

        var isMultiAgent = agentProfiles.Count > 1;

        foreach (var profile in agentProfiles)
        {
            var agent = new AgentContext(
                profile, llmBridge, options,
                registerMailbox: isMultiAgent,
                configureApps: configureApps);
            _agents[profile.Id] = agent;
            _pendingMessagesByAgent[profile.Id] = new Queue<ChannelMessage>();
        }

        if (isMultiAgent)
        {
            SetupChannelBridges();
        }
    }

    /// <summary>
    /// Setup per-agent forwarder that only enqueues cross-agent deliveries.
    /// Delivery happens later inside target agent serialized context.
    /// </summary>
    private void SetupChannelBridges()
    {
        foreach (var agent in _agents.Values)
        {
            var sourceAgentId = agent.AgentId;

            agent.LocalMessageChannel.SetForwarder((_, message) =>
            {
                var recipients = ResolveRecipients(sourceAgentId, message);

                lock (_sync)
                {
                    foreach (var agentId in recipients)
                    {
                        if (_pendingMessagesByAgent.TryGetValue(agentId, out var queue))
                        {
                            queue.Enqueue(message);
                        }

                        EnqueueWakeup_NoLock(agentId);
                    }
                }
            });
        }
    }

    private List<string> ResolveRecipients(string sourceAgentId, ChannelMessage message)
    {
        if (message.TargetAgentIds == null || message.TargetAgentIds.Count == 0)
        {
            return _agents.Keys
                .Where(id => !string.Equals(id, sourceAgentId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var targets = new HashSet<string>(message.TargetAgentIds, StringComparer.OrdinalIgnoreCase);
        return _agents.Keys
            .Where(id =>
                !string.Equals(id, sourceAgentId, StringComparison.OrdinalIgnoreCase) &&
                targets.Contains(id))
            .ToList();
    }

    private void EnqueueWakeup_NoLock(string agentId)
    {
        if (!_queuedAgentIds.Add(agentId))
        {
            return;
        }

        _wakeupQueue.Enqueue(new AgentWakeup
        {
            AgentId = agentId,
            TriggerMessage = "You have received new messages. Check your mailbox window to read and respond."
        });
    }

    public AgentContext? GetAgent(string agentId)
        => _agents.GetValueOrDefault(agentId);

    public IEnumerable<AgentContext> GetAllAgents()
        => _agents.Values;

    public int AgentCount => _agents.Count;

    public async Task<InteractionResult> InteractAsync(
        string agentId, string message, CancellationToken ct = default)
    {
        var agent = GetAgent(agentId)
            ?? throw new InvalidOperationException($"Agent '{agentId}' does not exist.");

        var result = await agent.RunSerializedAsync(
            () => agent.Interaction.ProcessAsync(message, ct), ct);

        await ProcessWakeupQueueAsync(ct);

        return result;
    }

    public async Task<InteractionResult> SimulateAsync(
        string agentId, string assistantOutput, CancellationToken ct = default)
    {
        var agent = GetAgent(agentId)
            ?? throw new InvalidOperationException($"Agent '{agentId}' does not exist.");

        var result = await agent.RunSerializedAsync(
            () => agent.Interaction.ProcessAssistantOutputAsync(assistantOutput, ct), ct);

        await ProcessWakeupQueueAsync(ct);

        return result;
    }

    private async Task ProcessWakeupQueueAsync(CancellationToken ct)
    {
        var depth = 0;

        while (depth < MaxWakeupLoopDepth)
        {
            ct.ThrowIfCancellationRequested();

            AgentWakeup? wakeup;
            List<ChannelMessage> pendingMessages = [];

            lock (_sync)
            {
                if (_wakeupQueue.Count == 0)
                {
                    break;
                }

                wakeup = _wakeupQueue.Dequeue();
                _queuedAgentIds.Remove(wakeup.AgentId);

                if (_pendingMessagesByAgent.TryGetValue(wakeup.AgentId, out var queue))
                {
                    while (queue.Count > 0)
                    {
                        pendingMessages.Add(queue.Dequeue());
                    }
                }
            }

            if (wakeup == null)
            {
                continue;
            }

            var agent = GetAgent(wakeup.AgentId);
            if (agent == null)
            {
                continue;
            }

            await agent.RunSerializedAsync(
                async () =>
                {
                    foreach (var message in pendingMessages)
                    {
                        agent.LocalMessageChannel.DeliverExternal(message);
                    }

                    return await agent.Interaction.ProcessAsync(wakeup.TriggerMessage, ct);
                },
                ct);

            depth++;
        }

        lock (_sync)
        {
            if (_wakeupQueue.Count > 0)
            {
                Trace.TraceWarning(
                    $"Session '{SessionId}' wakeup queue depth exceeded {MaxWakeupLoopDepth}. Remaining wakeups: {_wakeupQueue.Count}.");
            }
        }
    }

    // ========== 快照支持 ==========

    /// <summary>
    /// 采集会话快照（包含所有 Agent）。
    /// </summary>
    public SessionSnapshot TakeSnapshot()
    {
        var snapshot = new SessionSnapshot
        {
            SessionId = SessionId,
            CreatedAt = CreatedAt,
            SnapshotAt = DateTime.UtcNow,
            Version = 1,
            Agents = []
        };

        foreach (var agent in _agents.Values)
        {
            snapshot.Agents.Add(agent.TakeSnapshot());
        }

        return snapshot;
    }

    /// <summary>
    /// 从快照恢复会话状态。
    /// 仅恢复已存在的 Agent（快照中存在但当前 Session 中没有的会跳过）。
    /// </summary>
    public void RestoreFromSnapshot(SessionSnapshot snapshot)
    {
        foreach (var agentSnapshot in snapshot.Agents)
        {
            if (_agents.TryGetValue(agentSnapshot.Profile.Id, out var agent))
            {
                agent.RestoreFromSnapshot(agentSnapshot);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var agent in _agents.Values)
        {
            agent.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}

public class AgentWakeup
{
    public required string AgentId { get; init; }
    public required string TriggerMessage { get; init; }
}
