using ACI.Core.Abstractions;
using ACI.Core.Models;

namespace ACI.Framework.Runtime;

/// <summary>
/// 操作定义（Framework 层）
/// </summary>
public class ContextAction
{
    /// <summary>
    /// 操作 ID
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 操作标签
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// 操作处理器
    /// </summary>
    public required Func<ActionContext, Task<ActionResult>> Handler { get; init; }

    /// <summary>
    /// 参数列表
    /// </summary>
    public List<ContextParam> Parameters { get; init; } = [];

    /// <summary>
    /// 执行模式（默认同步）
    /// </summary>
    public ActionExecutionMode Mode { get; init; } = ActionExecutionMode.Sync;

    /// <summary>
    /// 添加参数（链式调用）
    /// </summary>
    public ContextAction WithParam(string name, ParamType type, bool required = true)
    {
        Parameters.Add(new ContextParam
        {
            Name = name,
            Type = type,
            Required = required
        });
        return this;
    }

    /// <summary>
    /// 将操作标记为异步执行模式。
    /// </summary>
    public ContextAction AsAsync()
    {
        return new ContextAction
        {
            Id = Id,
            Label = Label,
            Handler = Handler,
            Parameters = [.. Parameters],
            Mode = ActionExecutionMode.Async
        };
    }

    /// <summary>
    /// 转换为 Core 层的 ActionDefinition
    /// </summary>
    public ActionDefinition ToActionDefinition()
    {
        return new ActionDefinition
        {
            Id = Id,
            Label = Label,
            Mode = Mode,
            Parameters = Parameters.Select(p => new ParameterDefinition
            {
                Name = p.Name,
                Type = p.Type.ToString().ToLower(),
                Required = p.Required,
                Default = p.Default
            }).ToList()
        };
    }
}

/// <summary>
/// 参数定义
/// </summary>
public class ContextParam
{
    public required string Name { get; init; }
    public required ParamType Type { get; init; }
    public bool Required { get; init; } = true;
    public object? Default { get; init; }
}

/// <summary>
/// 参数类型
/// </summary>
public enum ParamType
{
    String,
    Int,
    Bool,
    Float
}
