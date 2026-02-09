using ACI.Core.Abstractions;
using ACI.Core.Models;
using ACI.Core.Services;
using ACI.LLM.Abstractions;
using ACI.LLM.Services;

namespace ACI.LLM;

internal sealed class ToolCallExecution
{
    public required ParsedAction Action { get; init; }
    public required ActionResult Result { get; init; }
    public required InteractionStep Step { get; init; }
}

/// <summary>
/// Runs interaction turns and tool-call loops.
/// </summary>
internal sealed class InteractionOrchestrator
{
    private readonly ILLMBridge _llm;
    private readonly IContextManager _contextManager;
    private readonly IWindowManager _windowManager;
    private readonly IContextRenderer _renderer;
    private readonly RenderOptions _renderOptions;
    private readonly int _maxAutoToolCallTurns;
    private readonly Action _ensureInitialized;
    private readonly Func<ParsedAction, int, int, CancellationToken, Task<ToolCallExecution>> _executeToolCallAsync;

    public InteractionOrchestrator(
        ILLMBridge llm,
        IContextManager contextManager,
        IWindowManager windowManager,
        IContextRenderer renderer,
        RenderOptions renderOptions,
        Action ensureInitialized,
        Func<ParsedAction, int, int, CancellationToken, Task<ToolCallExecution>> executeToolCallAsync,
        int maxAutoToolCallTurns)
    {
        _llm = llm;
        _contextManager = contextManager;
        _windowManager = windowManager;
        _renderer = renderer;
        _renderOptions = renderOptions;
        _ensureInitialized = ensureInitialized;
        _executeToolCallAsync = executeToolCallAsync;
        _maxAutoToolCallTurns = maxAutoToolCallTurns;
    }

    public async Task<InteractionResult> ProcessUserMessageAsync(string userMessage, CancellationToken ct = default)
    {
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

        for (var turn = 0; turn <= _maxAutoToolCallTurns; turn++)
        {
            PruneContext();

            var activeItems = _contextManager.GetActive();
            var messages = _renderer.Render(activeItems, _windowManager, _renderOptions);
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

        PruneContext();
        return InteractionResult.Fail(
            $"executed {_maxAutoToolCallTurns + 1} consecutive tool_call turns without a non-tool response");
    }

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

    private void PruneContext()
    {
        _contextManager.Prune(
            _windowManager,
            _renderOptions.MaxTokens,
            _renderOptions.MinConversationTokens,
            _renderOptions.PruneTargetTokens);
    }

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
