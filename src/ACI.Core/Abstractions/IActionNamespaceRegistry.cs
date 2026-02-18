using ACI.Core.Models;

namespace ACI.Core.Abstractions;

/// <summary>
/// Action 命名空间注册表接口。
/// </summary>
public interface IActionNamespaceRegistry
{
    /// <summary>
    /// 新增或覆盖命名空间定义。
    /// </summary>
    void Upsert(ActionNamespaceDefinition definition);

    /// <summary>
    /// 获取命名空间定义。
    /// </summary>
    bool TryGet(string namespaceId, out ActionNamespaceDefinition? definition);

    /// <summary>
    /// 获取全部命名空间定义。
    /// </summary>
    IReadOnlyList<ActionNamespaceDefinition> GetAll();

    /// <summary>
    /// 按命名空间 ID 批量获取定义。
    /// </summary>
    IReadOnlyList<ActionNamespaceDefinition> GetByIds(IEnumerable<string> namespaceIds);

    /// <summary>
    /// 查询命名空间下的 Action 定义。
    /// </summary>
    bool TryGetAction(string namespaceId, string actionId, out ActionDescriptor? action);
}
