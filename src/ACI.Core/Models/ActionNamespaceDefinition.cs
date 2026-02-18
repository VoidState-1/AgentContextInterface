using System.Text.Json;

namespace ACI.Core.Models;

/// <summary>
/// 命名空间下的 Action 描述。
/// </summary>
public sealed class ActionDescriptor
{
    /// <summary>
    /// Action 标识（命名空间内唯一）。
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 参数定义，键为参数名，值为签名字符串（如 string / string? / array<string>）。
    /// </summary>
    public Dictionary<string, string> Params { get; init; } = [];

    /// <summary>
    /// Action 说明。
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 执行模式（运行时元数据，不直接暴露给模型）。
    /// </summary>
    public ActionExecutionMode Mode { get; init; } = ActionExecutionMode.Sync;

    /// <summary>
    /// 生成用于提示词渲染的对象（仅包含 id / params / description）。
    /// </summary>
    public object ToPromptObject()
    {
        return new
        {
            id = Id,
            @params = Params,
            description = Description
        };
    }
}

/// <summary>
/// Action 命名空间定义。
/// </summary>
public sealed class ActionNamespaceDefinition
{
    /// <summary>
    /// 命名空间标识。
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 命名空间内的 Action 列表。
    /// </summary>
    public List<ActionDescriptor> Actions { get; init; } = [];

    /// <summary>
    /// 将 Action 列表渲染为 JSON 字符串。
    /// </summary>
    public string RenderPromptJson()
    {
        var promptActions = Actions.Select(t => t.ToPromptObject()).ToList();
        return JsonSerializer.Serialize(promptActions);
    }
}
