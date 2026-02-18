using ACI.Core.Abstractions;
using ACI.Core.Models;
using ACI.Core.Services;
using ACI.Framework.Components;

namespace ACI.Framework.Runtime;

/// <summary>
/// Framework 层窗口定义。
/// </summary>
public class ContextWindow
{
    /// <summary>
    /// 窗口 ID（应用内唯一）。
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 窗口描述。
    /// </summary>
    public IComponent? Description { get; init; }

    /// <summary>
    /// 窗口内容。
    /// </summary>
    public required IComponent Content { get; init; }

    /// <summary>
    /// 窗口引用的 Action 命名空间。
    /// </summary>
    public List<string> NamespaceRefs { get; init; } = [];

    /// <summary>
    /// 窗口配置。
    /// </summary>
    public WindowOptions Options { get; init; } = new();

    /// <summary>
    /// 转换为 Core 层 Window。
    /// </summary>
    public Window ToWindow()
    {
        return new Window
        {
            Id = Id,
            Description = Description,
            Content = Content,
            NamespaceRefs = NamespaceRefs.ToList(),
            Options = Options,
            Handler = null
        };
    }
}

/// <summary>
/// 基于 ContextAction 的处理器。
/// </summary>
internal sealed class ContextActionHandler : IActionHandler
{
    /// <summary>
    /// 动作映射表。
    /// </summary>
    private readonly Dictionary<string, ContextAction> _actions;

    /// <summary>
    /// 构建处理器。
    /// </summary>
    public ContextActionHandler(IEnumerable<ContextAction> actions)
    {
        _actions = actions.ToDictionary(a => a.Id);
    }

    /// <summary>
    /// 执行动作。
    /// </summary>
    public async Task<ActionResult> ExecuteAsync(ActionContext context)
    {
        if (!_actions.TryGetValue(context.ActionId, out var action))
        {
            var normalizedActionId = NormalizeActionId(context.ActionId);
            if (!_actions.TryGetValue(normalizedActionId, out action))
            {
                return ActionResult.Fail($"Action '{context.ActionId}' not found");
            }
        }

        var normalizedContext = new ActionContext
        {
            Window = context.Window,
            ActionId = action.Id,
            Parameters = context.Parameters
        };

        var validationError = ActionParamValidator.Validate(action.Params, normalizedContext.Parameters);
        if (validationError != null)
        {
            return ActionResult.Fail(validationError);
        }

        return await action.Handler(normalizedContext);
    }

    /// <summary>
    /// 归一化动作 ID（支持 namespace.action）。
    /// </summary>
    private static string NormalizeActionId(string actionId)
    {
        if (string.IsNullOrWhiteSpace(actionId))
        {
            return actionId;
        }

        var dotIndex = actionId.IndexOf('.');
        if (dotIndex <= 0 || dotIndex >= actionId.Length - 1)
        {
            return actionId;
        }

        return actionId[(dotIndex + 1)..];
    }
}
