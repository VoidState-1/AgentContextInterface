using ACI.Core.Models;

namespace ACI.Core.Abstractions;

/// <summary>
/// 工具命名空间注册表接口。
/// </summary>
public interface IToolNamespaceRegistry
{
    /// <summary>
    /// 新增或覆盖命名空间定义。
    /// </summary>
    void Upsert(ToolNamespaceDefinition definition);

    /// <summary>
    /// 获取命名空间定义。
    /// </summary>
    bool TryGet(string namespaceId, out ToolNamespaceDefinition? definition);

    /// <summary>
    /// 获取所有命名空间定义。
    /// </summary>
    IReadOnlyList<ToolNamespaceDefinition> GetAll();

    /// <summary>
    /// 按命名空间 ID 批量获取定义。
    /// </summary>
    IReadOnlyList<ToolNamespaceDefinition> GetByIds(IEnumerable<string> namespaceIds);

    /// <summary>
    /// 查找命名空间下的工具定义。
    /// </summary>
    bool TryGetTool(string namespaceId, string toolId, out ToolDescriptor? tool);
}
