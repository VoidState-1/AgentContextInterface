using ACI.Core.Abstractions;
using ACI.Core.Models;

namespace ACI.Framework.Runtime;

/// <summary>
/// Framework 层的 Action 定义。
/// </summary>
public class ContextAction
{
    /// <summary>
    /// Action ID（命名空间内唯一）。
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Action 描述（用于提示词渲染）。
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Action 执行处理器。
    /// </summary>
    public required Func<ActionContext, Task<ActionResult>> Handler { get; init; }

    /// <summary>
    /// 参数结构定义。
    /// </summary>
    public ActionParamSchema? Params { get; init; }

    /// <summary>
    /// 执行模式（同步/异步）。
    /// </summary>
    public ActionExecutionMode Mode { get; init; } = ActionExecutionMode.Sync;

    /// <summary>
    /// 返回异步执行版本。
    /// </summary>
    public ContextAction AsAsync()
    {
        return new ContextAction
        {
            Id = Id,
            Description = Description,
            Handler = Handler,
            Params = Params,
            Mode = ActionExecutionMode.Async
        };
    }
}
