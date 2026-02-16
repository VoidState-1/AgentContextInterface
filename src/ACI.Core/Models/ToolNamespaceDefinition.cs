using System.Text.Json;

namespace ACI.Core.Models;

/// <summary>
/// 命名空间下的工具描述。
/// </summary>
public sealed class ToolDescriptor
{
    /// <summary>
    /// 工具标识（命名空间内唯一）。
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 参数定义，键为参数名，值为简写类型（如 string / string? / array<string>）。
    /// </summary>
    public Dictionary<string, string> Params { get; init; } = [];

    /// <summary>
    /// 工具说明。
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
/// 工具命名空间定义。
/// </summary>
public sealed class ToolNamespaceDefinition
{
    /// <summary>
    /// 命名空间标识。
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 命名空间内的工具列表。
    /// </summary>
    public List<ToolDescriptor> Tools { get; init; } = [];

    /// <summary>
    /// 将工具列表渲染为 JSON 字符串。
    /// </summary>
    public string RenderPromptJson()
    {
        var promptTools = Tools.Select(t => t.ToPromptObject()).ToList();
        return JsonSerializer.Serialize(promptTools);
    }
}
