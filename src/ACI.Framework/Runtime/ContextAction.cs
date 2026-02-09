using ACI.Core.Abstractions;
using ACI.Core.Models;

namespace ACI.Framework.Runtime;

/// <summary>
/// Action definition in Framework layer.
/// </summary>
public class ContextAction
{
    public required string Id { get; init; }

    public required string Label { get; init; }

    public required Func<ActionContext, Task<ActionResult>> Handler { get; init; }

    /// <summary>
    /// Unified JSON-like parameter schema.
    /// </summary>
    public ActionParamSchema? Params { get; init; }

    public ActionExecutionMode Mode { get; init; } = ActionExecutionMode.Sync;

    public ContextAction AsAsync()
    {
        return new ContextAction
        {
            Id = Id,
            Label = Label,
            Handler = Handler,
            Params = Params,
            Mode = ActionExecutionMode.Async
        };
    }

    public ActionDefinition ToActionDefinition()
    {
        return new ActionDefinition
        {
            Id = Id,
            Label = Label,
            Mode = Mode,
            ParamsSchema = Params
        };
    }
}
