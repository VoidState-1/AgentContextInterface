using ACI.Core.Abstractions;
using ACI.Core.Models;
using ACI.Core.Services;
using ACI.Framework.Runtime;
using ACI.LLM.Abstractions;
using System.Text;
using System.Text.Json;

namespace ACI.LLM;

/// <summary>
/// Facade for interaction APIs. Loop orchestration is delegated to InteractionOrchestrator.
/// </summary>
public class InteractionController
{
    private const int MaxAutoToolCallTurns = 12;

    private readonly FrameworkHost _host;
    private readonly IContextManager _contextManager;
    private readonly IWindowManager _windowManager;
    private readonly ActionExecutor _actionExecutor;
    private readonly IContextRenderer _renderer;
    private readonly RenderOptions _renderOptions;
    private readonly InteractionOrchestrator _orchestrator;

    private bool _initialized;

    public InteractionController(
        ILLMBridge llm,
        FrameworkHost host,
        IContextManager contextManager,
        IWindowManager windowManager,
        ActionExecutor actionExecutor,
        IContextRenderer? renderer = null,
        RenderOptions? renderOptions = null)
    {
        _host = host;
        _contextManager = contextManager;
        _windowManager = windowManager;
        _actionExecutor = actionExecutor;
        _renderer = renderer ?? new ContextRenderer();
        _renderOptions = renderOptions ?? new RenderOptions();

        _orchestrator = new InteractionOrchestrator(
            llm,
            _contextManager,
            _windowManager,
            _renderer,
            _renderOptions,
            EnsureInitialized,
            ExecuteToolCallAsync,
            MaxAutoToolCallTurns);
    }

    public Task<InteractionResult> ProcessAsync(string userMessage, CancellationToken ct = default)
        => _orchestrator.ProcessUserMessageAsync(userMessage, ct);

    public Task<InteractionResult> ProcessAssistantOutputAsync(string assistantOutput, CancellationToken ct = default)
        => _orchestrator.ProcessAssistantOutputAsync(assistantOutput, ct);

    public IReadOnlyList<LlmMessage> GetCurrentLlmInputSnapshot()
    {
        EnsureInitialized();
        PruneContext();
        var activeItems = _contextManager.GetActive();
        return _renderer.Render(activeItems, _windowManager, _renderOptions);
    }

    public string GetCurrentLlmInputRaw()
    {
        var messages = GetCurrentLlmInputSnapshot();

        var sb = new StringBuilder();
        foreach (var msg in messages)
        {
            sb.AppendLine($"[{msg.Role}]");
            sb.AppendLine(msg.Content);
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    public async Task<ActionResult> ExecuteWindowActionAsync(
        string windowId,
        string actionId,
        JsonElement? parameters = null)
    {
        if (string.IsNullOrWhiteSpace(windowId) || string.IsNullOrWhiteSpace(actionId))
        {
            return ActionResult.Fail("action is missing required fields");
        }

        var result = await _actionExecutor.ExecuteAsync(windowId, actionId, parameters);
        if (!result.Success)
        {
            return result;
        }

        if (!TryExtractLaunchCommand(result.Data, out var appName, out var target, out var closeSource))
        {
            PruneContext();
            return result;
        }

        if (string.IsNullOrWhiteSpace(appName))
        {
            return ActionResult.Fail("launch command missing app name");
        }

        try
        {
            _host.Launch(appName, target);

            if (closeSource)
            {
                await _actionExecutor.ExecuteAsync(
                    windowId,
                    "close",
                    JsonSerializer.SerializeToElement(new
                    {
                        summary = $"Opened app {appName} from window {windowId}"
                    }));
            }

            PruneContext();
            return result;
        }
        catch (Exception ex)
        {
            return ActionResult.Fail($"launch failed: {ex.Message}");
        }
    }

    private async Task<ToolCallExecution> ExecuteToolCallAsync(
        ParsedAction action,
        int turn,
        int index,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var resolvedMode = ResolveActionMode(action.WindowId, action.ActionId);
        var callId = $"call_{turn}_{index}";
        var actionResult = await ExecuteWindowActionAsync(action.WindowId, action.ActionId, action.Parameters);

        var step = new InteractionStep
        {
            CallId = callId,
            WindowId = action.WindowId,
            ActionId = action.ActionId,
            ResolvedMode = resolvedMode == ActionExecutionMode.Async ? "async" : "sync",
            Success = actionResult.Success,
            Message = actionResult.Message,
            Summary = actionResult.Summary,
            TaskId = TryExtractTaskId(actionResult.Data),
            Turn = turn,
            Index = index
        };

        return new ToolCallExecution
        {
            Action = action,
            Result = actionResult,
            Step = step
        };
    }

    private ActionExecutionMode ResolveActionMode(string windowId, string actionId)
    {
        if (string.Equals(actionId, "close", StringComparison.OrdinalIgnoreCase))
        {
            return ActionExecutionMode.Sync;
        }

        var window = _windowManager.Get(windowId);
        var action = window?.Actions.FirstOrDefault(a => string.Equals(a.Id, actionId, StringComparison.Ordinal));
        return action?.Mode ?? ActionExecutionMode.Sync;
    }

    private void PruneContext()
    {
        _contextManager.Prune(
            _windowManager,
            _renderOptions.MaxTokens,
            _renderOptions.MinConversationTokens,
            _renderOptions.TrimToTokens);
    }

    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        _contextManager.Add(new ContextItem
        {
            Id = "system_prompt",
            Type = ContextItemType.System,
            Content = PromptBuilder.BuildSystemPrompt()
        });

        _initialized = true;
    }

    private static bool TryExtractLaunchCommand(
        object? data,
        out string? appName,
        out string? target,
        out bool closeSource)
    {
        appName = null;
        target = null;
        closeSource = false;

        if (data == null)
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(data));
            var root = doc.RootElement;

            if (!root.TryGetProperty("action", out var actionElement) ||
                actionElement.ValueKind != JsonValueKind.String ||
                !string.Equals(actionElement.GetString(), "launch", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (root.TryGetProperty("app", out var appElement) && appElement.ValueKind == JsonValueKind.String)
            {
                appName = appElement.GetString();
            }

            if (root.TryGetProperty("target", out var targetElement) && targetElement.ValueKind == JsonValueKind.String)
            {
                target = targetElement.GetString();
            }

            if (root.TryGetProperty("close_source", out var closeElement) &&
                (closeElement.ValueKind == JsonValueKind.True || closeElement.ValueKind == JsonValueKind.False))
            {
                closeSource = closeElement.GetBoolean();
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? TryExtractTaskId(object? data)
    {
        if (data == null)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(data));
            var root = doc.RootElement;
            if (!root.TryGetProperty("task_id", out var taskIdElement) ||
                taskIdElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return taskIdElement.GetString();
        }
        catch
        {
            return null;
        }
    }
}

public class InteractionResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public string? Response { get; init; }
    public ParsedAction? Action { get; init; }
    public ActionResult? ActionResult { get; init; }
    public TokenUsage? Usage { get; init; }
    public IReadOnlyList<InteractionStep>? Steps { get; init; }

    public static InteractionResult Ok(
        string response,
        ParsedAction? action = null,
        ActionResult? actionResult = null,
        TokenUsage? usage = null,
        IReadOnlyList<InteractionStep>? steps = null) =>
        new()
        {
            Success = true,
            Response = response,
            Action = action,
            ActionResult = actionResult,
            Usage = usage,
            Steps = steps
        };

    public static InteractionResult Fail(string error)
        => new() { Success = false, Error = error };
}

public class InteractionStep
{
    public required string CallId { get; init; }
    public required string WindowId { get; init; }
    public required string ActionId { get; init; }
    public required string ResolvedMode { get; init; }
    public required bool Success { get; init; }
    public string? Message { get; init; }
    public string? Summary { get; init; }
    public string? TaskId { get; init; }
    public int Turn { get; init; }
    public int Index { get; init; }
}
