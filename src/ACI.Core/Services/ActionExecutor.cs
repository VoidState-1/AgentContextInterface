using ACI.Core.Abstractions;
using ACI.Core.Models;
using System.Text.Json;

namespace ACI.Core.Services;

/// <summary>
/// Executes window actions and publishes execution events.
/// </summary>
public class ActionExecutor
{
    private readonly IWindowManager _windows;
    private readonly ISeqClock _clock;
    private readonly IEventBus _events;
    private readonly Action<string>? _refreshWindow;

    public ActionExecutor(
        IWindowManager windows,
        ISeqClock clock,
        IEventBus events,
        Action<string>? refreshWindow = null)
    {
        _windows = windows;
        _clock = clock;
        _events = events;
        _refreshWindow = refreshWindow;
    }

    /// <summary>
    /// Executes one action on one window.
    /// </summary>
    public async Task<ActionResult> ExecuteAsync(
        string windowId,
        string actionId,
        JsonElement? parameters = null)
    {
        var window = _windows.Get(windowId);
        if (window == null)
        {
            return ActionResult.Fail($"Window '{windowId}' does not exist");
        }

        // Reserved system close action.
        if (actionId == "close")
        {
            if (!window.Options.Closable)
            {
                return ActionResult.Fail($"Window '{windowId}' cannot be closed");
            }

            var seq = _clock.Next();
            var summary = TryGetSummary(parameters);

            _windows.Remove(windowId);

            var closeResult = ActionResult.Close(summary);
            closeResult.LogSeq = seq;

            _events.Publish(new ActionExecutedEvent(
                Seq: seq,
                WindowId: windowId,
                ActionId: actionId,
                Success: true,
                Summary: summary
            ));

            return closeResult;
        }

        var actionDef = window.Actions.FirstOrDefault(a => a.Id == actionId);
        if (actionDef == null)
        {
            return ActionResult.Fail($"Action '{actionId}' does not exist on window '{windowId}'");
        }

        var validationError = ValidateParameters(actionDef, parameters);
        if (validationError != null)
        {
            return ActionResult.Fail(validationError);
        }

        var seq2 = _clock.Next();

        ActionResult result;
        if (window.Handler != null)
        {
            var context = new ActionContext
            {
                Window = window,
                ActionId = actionId,
                Parameters = parameters
            };

            try
            {
                result = await window.Handler.ExecuteAsync(context);
            }
            catch (Exception ex)
            {
                result = ActionResult.Fail($"Action execution failed: {ex.Message}");
            }
        }
        else
        {
            result = ActionResult.Ok();
        }

        result.LogSeq = seq2;

        _events.Publish(new ActionExecutedEvent(
            Seq: seq2,
            WindowId: windowId,
            ActionId: actionId,
            Success: result.Success,
            Summary: result.Summary
        ));

        if (result.ShouldClose || window.Options.AutoCloseOnAction)
        {
            _windows.Remove(windowId);
        }
        else if (result.ShouldRefresh)
        {
            if (_refreshWindow != null)
            {
                _refreshWindow(windowId);
            }
            else if (_windows is WindowManager wm)
            {
                wm.NotifyUpdated(windowId);
            }
        }

        return result;
    }

    private static string? ValidateParameters(
        ActionDefinition actionDef,
        JsonElement? parameters)
    {
        if (parameters.HasValue && parameters.Value.ValueKind != JsonValueKind.Object)
        {
            return "params must be a JSON object";
        }

        foreach (var param in actionDef.Parameters.Where(p => p.Required))
        {
            if (!parameters.HasValue || !parameters.Value.TryGetProperty(param.Name, out _))
            {
                return $"Missing required parameter: {param.Name}";
            }
        }

        return null;
    }

    private static string? TryGetSummary(JsonElement? parameters)
    {
        if (!parameters.HasValue || parameters.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!parameters.Value.TryGetProperty("summary", out var summaryElement))
        {
            return null;
        }

        return summaryElement.ValueKind == JsonValueKind.String
            ? summaryElement.GetString()
            : summaryElement.ToString();
    }
}

/// <summary>
/// Event emitted after an action finishes.
/// </summary>
public record ActionExecutedEvent(
    int Seq,
    string WindowId,
    string ActionId,
    bool Success,
    string? Summary
) : IEvent;

/// <summary>
/// Event emitted when an app is launched.
/// </summary>
public record AppCreatedEvent(
    int Seq,
    string AppName,
    string? Target,
    bool Success
) : IEvent;
