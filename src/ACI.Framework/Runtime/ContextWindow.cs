using ACI.Core.Abstractions;
using ACI.Core.Models;
using ACI.Framework.Components;

namespace ACI.Framework.Runtime;

/// <summary>
/// 窗口定义（Framework 层）
/// </summary>
public class ContextWindow
{
    /// <summary>
    /// 窗口 ID（应用内唯一）
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 窗口描述（告诉 AI 这是什么、怎么操作）
    /// </summary>
    public IComponent? Description { get; init; }

    /// <summary>
    /// 窗口主内容
    /// </summary>
    public required IComponent Content { get; init; }

    /// <summary>
    /// 可用操作列表
    /// </summary>
    public List<ContextAction> Actions { get; init; } = [];

    /// <summary>
    /// 窗口配置
    /// </summary>
    public WindowOptions Options { get; init; } = new();

    /// <summary>
    /// 转换为 Core 层的 Window
    /// </summary>
    public Window ToWindow()
    {
        return new Window
        {
            Id = Id,
            Description = Description,
            Content = Content,
            Actions = Actions.Select(a => a.ToActionDefinition()).ToList(),
            Options = Options,
            Handler = new ContextActionHandler(Actions)
        };
    }
}

/// <summary>
/// 操作处理器（内部类）
/// </summary>
internal class ContextActionHandler : IActionHandler
{
    private readonly Dictionary<string, ContextAction> _actions;

    public ContextActionHandler(IEnumerable<ContextAction> actions)
    {
        _actions = actions.ToDictionary(a => a.Id);
    }

    public async Task<ActionResult> ExecuteAsync(ActionContext context)
    {
        if (_actions.TryGetValue(context.ActionId, out var action))
        {
            return await action.Handler(context);
        }
        return ActionResult.Fail($"操作 '{context.ActionId}' 未找到");
    }
}
