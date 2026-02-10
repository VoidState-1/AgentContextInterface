using ACI.Core.Abstractions;
using ACI.Core.Models;

namespace ACI.Tests.Common.Fakes;

public sealed class FakeActionHandler : IActionHandler
{
    private readonly Func<ActionContext, Task<ActionResult>> _execute;

    public List<ActionContext> Calls { get; } = [];

    public FakeActionHandler(Func<ActionContext, Task<ActionResult>>? execute = null)
    {
        _execute = execute ?? (_ => Task.FromResult(ActionResult.Ok()));
    }

    public Task<ActionResult> ExecuteAsync(ActionContext context)
    {
        Calls.Add(context);
        return _execute(context);
    }
}

