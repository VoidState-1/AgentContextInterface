using ACI.Core.Abstractions;
using ACI.Core.Models;
using ACI.Core.Services;
using ACI.LLM.Abstractions;
using ACI.LLM.Services;

namespace ACI.LLM;

/// <summary>
/// 单次工具调用执行结果。
/// </summary>
internal sealed class ToolCallExecution
{
    /// <summary>
    /// 解析后的工具调用动作。
    /// </summary>
    public required ParsedAction Action { get; init; }

    /// <summary>
    /// 动作执行结果。
    /// </summary>
    public required ActionResult Result { get; init; }

    /// <summary>
    /// 用于回传给前端的步骤记录。
    /// </summary>
    public required InteractionStep Step { get; init; }
}

/// <summary>
/// 运行交互轮次与工具调用循环。
/// </summary>
internal sealed class InteractionOrchestrator
{
    /// <summary>
    /// 核心依赖。
    /// </summary>
    private readonly ILLMBridge _llm;
    private readonly IContextManager _contextManager;
    private readonly IWindowManager _windowManager;
    private readonly IToolNamespaceRegistry? _toolNamespaces;
    private readonly IContextRenderer _renderer;

    /// <summary>
    /// 循环配置与委托。
    /// </summary>
    private readonly RenderOptions _renderOptions;
    private readonly int _maxAutoToolCallTurns;
    private readonly Action _ensureInitialized;
    private readonly Func<ParsedAction, int, int, CancellationToken, Task<ToolCallExecution>> _executeToolCallAsync;

    /// <summary>
    /// 创建交互编排器。
    /// </summary>
    public InteractionOrchestrator(
        ILLMBridge llm,
        IContextManager contextManager,
        IWindowManager windowManager,
        IToolNamespaceRegistry? toolNamespaces,
        IContextRenderer renderer,
        RenderOptions renderOptions,
        Action ensureInitialized,
        Func<ParsedAction, int, int, CancellationToken, Task<ToolCallExecution>> executeToolCallAsync,
        int maxAutoToolCallTurns)
    {
        _llm = llm;
        _contextManager = contextManager;
        _windowManager = windowManager;
        _toolNamespaces = toolNamespaces;
        _renderer = renderer;
        _renderOptions = renderOptions;
        _ensureInitialized = ensureInitialized;
        _executeToolCallAsync = executeToolCallAsync;
        _maxAutoToolCallTurns = maxAutoToolCallTurns;
    }

    /// <summary>
    /// 处理用户输入并驱动自动工具循环。
    /// </summary>
    public async Task<InteractionResult> ProcessUserMessageAsync(string userMessage, CancellationToken ct = default)
    {
        // 1. 初始化系统上下文并写入用户输入。
        _ensureInitialized();

        _contextManager.Add(new ContextItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = ContextItemType.User,
            Content = userMessage
        });

        ParsedAction? lastAction = null;
        ActionResult? lastActionResult = null;
        string lastResponseContent = string.Empty;
        var totalUsage = new TokenUsage();
        var steps = new List<InteractionStep>();

        // 2. 循环请求 LLM，直到返回非 tool_call 内容或达到轮次上限。
        for (var turn = 0; turn <= _maxAutoToolCallTurns; turn++)
        {
            PruneContext();

            var activeItems = _contextManager.GetActive();
            var messages = _renderer.Render(activeItems, _windowManager, _toolNamespaces, _renderOptions);
            var llmResponse = await _llm.SendAsync(messages, ct);
            if (!llmResponse.Success)
            {
                return InteractionResult.Fail(llmResponse.Error ?? "LLM call failed");
            }

            AccumulateUsage(totalUsage, llmResponse.Usage);

            lastResponseContent = llmResponse.Content ?? string.Empty;
            _contextManager.Add(new ContextItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = ContextItemType.Assistant,
                Content = lastResponseContent
            });

            // 3. 若返回普通文本则结束；若为 tool_call 则顺序执行每个调用。
            var parsedActionBatch = ActionParser.Parse(lastResponseContent);
            if (parsedActionBatch == null)
            {
                PruneContext();
                return InteractionResult.Ok(lastResponseContent, lastAction, lastActionResult, totalUsage, steps);
            }

            for (var index = 0; index < parsedActionBatch.Calls.Count; index++)
            {
                var parsedAction = parsedActionBatch.Calls[index];
                var execution = await _executeToolCallAsync(parsedAction, turn + 1, index + 1, ct);

                lastAction = execution.Action;
                lastActionResult = execution.Result;
                steps.Add(execution.Step);
            }
        }

        // 4. 超过自动循环上限后返回失败，避免无限循环。
        PruneContext();
        return InteractionResult.Fail(
            $"executed {_maxAutoToolCallTurns + 1} consecutive tool_call turns without a non-tool response");
    }

    /// <summary>
    /// 处理外部注入的 assistant 输出并执行其中的工具调用。
    /// </summary>
    public async Task<InteractionResult> ProcessAssistantOutputAsync(string assistantOutput, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _ensureInitialized();

        _contextManager.Add(new ContextItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = ContextItemType.Assistant,
            Content = assistantOutput
        });

        var parsedActionBatch = ActionParser.Parse(assistantOutput);
        ParsedAction? lastAction = null;
        ActionResult? lastActionResult = null;
        var steps = new List<InteractionStep>();

        if (parsedActionBatch != null)
        {
            for (var index = 0; index < parsedActionBatch.Calls.Count; index++)
            {
                var parsedAction = parsedActionBatch.Calls[index];
                var execution = await _executeToolCallAsync(parsedAction, 1, index + 1, ct);

                lastAction = execution.Action;
                lastActionResult = execution.Result;
                steps.Add(execution.Step);
            }
        }

        PruneContext();
        return InteractionResult.Ok(assistantOutput, lastAction, lastActionResult, steps: steps);
    }

    /// <summary>
    /// 调用上下文管理器执行裁剪。
    /// </summary>
    private void PruneContext()
    {
        _contextManager.Prune(
            _windowManager,
            _renderOptions.MaxTokens,
            _renderOptions.MinConversationTokens,
            _renderOptions.PruneTargetTokens);
    }

    /// <summary>
    /// 累加 Token 使用量。
    /// </summary>
    private static void AccumulateUsage(TokenUsage total, TokenUsage? delta)
    {
        if (delta == null)
        {
            return;
        }

        total.PromptTokens += delta.PromptTokens;
        total.CompletionTokens += delta.CompletionTokens;
        total.TotalTokens += delta.TotalTokens;
    }
}
