using ACI.Framework.Runtime;
using ACI.LLM;
using ACI.LLM.Abstractions;
using ACI.Server.Settings;

namespace ACI.Server.Services;

/// <summary>
/// 多 Agent 会话容器。
/// 管理多个 AgentContext，设置频道桥接，处理唤起队列。
/// </summary>
public class Session : IDisposable
{
    public string SessionId { get; }
    public DateTime CreatedAt { get; }

    private readonly Dictionary<string, AgentContext> _agents = [];
    private readonly Queue<AgentWakeup> _wakeupQueue = new();
    private bool _disposed;

    /// <summary>
    /// 消息队列循环的最大深度（防止无限循环）
    /// </summary>
    private const int MaxWakeupLoopDepth = 20;

    /// <summary>
    /// 创建 Session 并初始化所有 Agent。
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="agentProfiles">Agent 配置列表</param>
    /// <param name="llmBridge">LLM 桥接</param>
    /// <param name="options">ACI 配置</param>
    /// <param name="configureApps">外部应用注册回调</param>
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

        // 1. 创建所有 Agent
        foreach (var profile in agentProfiles)
        {
            var agent = new AgentContext(
                profile, llmBridge, options,
                registerMailbox: isMultiAgent,
                configureApps: configureApps);
            _agents[profile.Id] = agent;
        }

        // 2. 如果是多 Agent，设置频道桥接
        if (isMultiAgent)
        {
            SetupChannelBridges();
        }
    }

    /// <summary>
    /// 设置跨 Agent 频道桥接。
    /// 每个 Agent 发出 scope=Session 的消息时，转发给所有其他 Agent。
    /// </summary>
    private void SetupChannelBridges()
    {
        foreach (var agent in _agents.Values)
        {
            var sourceAgentId = agent.AgentId;

            agent.LocalMessageChannel.SetForwarder((_, message) =>
            {
                // 转发给所有其他 Agent
                foreach (var other in _agents.Values)
                {
                    if (other.AgentId != sourceAgentId)
                    {
                        other.LocalMessageChannel.DeliverExternal(message);
                    }
                }

                // 将接收方 Agent 加入唤起队列（去重）
                foreach (var other in _agents.Values)
                {
                    if (other.AgentId != sourceAgentId &&
                        !_wakeupQueue.Any(w => w.AgentId == other.AgentId))
                    {
                        _wakeupQueue.Enqueue(new AgentWakeup
                        {
                            AgentId = other.AgentId,
                            TriggerMessage = "You have received new messages. " +
                                             "Check your mailbox window to read and respond."
                        });
                    }
                }
            });
        }
    }

    // --- Agent 访问 ---

    public AgentContext? GetAgent(string agentId)
        => _agents.GetValueOrDefault(agentId);

    public IEnumerable<AgentContext> GetAllAgents()
        => _agents.Values;

    /// <summary>
    /// Agent 数量。
    /// </summary>
    public int AgentCount => _agents.Count;

    // --- 交互入口 ---

    /// <summary>
    /// 向指定 Agent 发送用户消息，并处理后续唤起队列。
    /// </summary>
    public async Task<InteractionResult> InteractAsync(
        string agentId, string message, CancellationToken ct = default)
    {
        var agent = GetAgent(agentId)
            ?? throw new InvalidOperationException($"Agent '{agentId}' does not exist.");

        // 1. 执行目标 Agent 的交互
        var result = await agent.RunSerializedAsync(
            () => agent.Interaction.ProcessAsync(message, ct), ct);

        // 2. 处理唤起队列（其他 Agent 可能被唤起）
        await ProcessWakeupQueueAsync(ct);

        return result;
    }

    /// <summary>
    /// 向指定 Agent 模拟注入 assistant 输出，并处理唤起队列。
    /// </summary>
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

    /// <summary>
    /// 唤起队列循环：逐个唤起有待处理消息的 Agent。
    /// 类似 InteractionOrchestrator 中的 tool_call 循环。
    /// </summary>
    private async Task ProcessWakeupQueueAsync(CancellationToken ct)
    {
        var depth = 0;

        while (_wakeupQueue.Count > 0 && depth < MaxWakeupLoopDepth)
        {
            ct.ThrowIfCancellationRequested();

            var wakeup = _wakeupQueue.Dequeue();
            var agent = GetAgent(wakeup.AgentId);
            if (agent == null) continue;

            await agent.RunSerializedAsync(
                () => agent.Interaction.ProcessAsync(wakeup.TriggerMessage, ct), ct);

            depth++;
        }

        // 如果还有未处理的唤起，清空（防止残留）
        _wakeupQueue.Clear();
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

/// <summary>
/// Agent 唤起请求
/// </summary>
public class AgentWakeup
{
    public required string AgentId { get; init; }
    public required string TriggerMessage { get; init; }
}
