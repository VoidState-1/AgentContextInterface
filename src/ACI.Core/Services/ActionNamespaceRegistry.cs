using ACI.Core.Abstractions;
using ACI.Core.Models;

namespace ACI.Core.Services;

/// <summary>
/// 内存 Action 命名空间注册表。
/// </summary>
public sealed class ActionNamespaceRegistry : IActionNamespaceRegistry
{
    /// <summary>
    /// 命名空间映射。
    /// </summary>
    private readonly Dictionary<string, ActionNamespaceDefinition> _namespaces =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 新增或覆盖命名空间定义。
    /// </summary>
    public void Upsert(ActionNamespaceDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (string.IsNullOrWhiteSpace(definition.Id))
        {
            throw new ArgumentException("Namespace id cannot be empty.", nameof(definition));
        }

        _namespaces[definition.Id] = Clone(definition);
    }

    /// <summary>
    /// 获取命名空间定义。
    /// </summary>
    public bool TryGet(string namespaceId, out ActionNamespaceDefinition? definition)
    {
        if (_namespaces.TryGetValue(namespaceId, out var existing))
        {
            definition = Clone(existing);
            return true;
        }

        definition = null;
        return false;
    }

    /// <summary>
    /// 获取全部命名空间定义。
    /// </summary>
    public IReadOnlyList<ActionNamespaceDefinition> GetAll()
    {
        return _namespaces.Values
            .OrderBy(n => n.Id, StringComparer.OrdinalIgnoreCase)
            .Select(Clone)
            .ToList();
    }

    /// <summary>
    /// 按 ID 批量获取命名空间定义。
    /// </summary>
    public IReadOnlyList<ActionNamespaceDefinition> GetByIds(IEnumerable<string> namespaceIds)
    {
        var ids = namespaceIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var result = new List<ActionNamespaceDefinition>();
        foreach (var id in ids)
        {
            if (_namespaces.TryGetValue(id, out var definition))
            {
                result.Add(Clone(definition));
            }
        }

        return result;
    }

    /// <summary>
    /// 查询命名空间下的 Action。
    /// </summary>
    public bool TryGetAction(string namespaceId, string actionId, out ActionDescriptor? action)
    {
        action = null;
        if (!_namespaces.TryGetValue(namespaceId, out var definition))
        {
            return false;
        }

        var matched = definition.Actions.FirstOrDefault(a =>
            string.Equals(a.Id, actionId, StringComparison.OrdinalIgnoreCase));

        if (matched == null)
        {
            return false;
        }

        action = Clone(matched);
        return true;
    }

    /// <summary>
    /// 深拷贝命名空间定义，避免外部修改内部状态。
    /// </summary>
    private static ActionNamespaceDefinition Clone(ActionNamespaceDefinition source)
    {
        return new ActionNamespaceDefinition
        {
            Id = source.Id,
            Actions = source.Actions.Select(Clone).ToList()
        };
    }

    /// <summary>
    /// 深拷贝 Action 定义。
    /// </summary>
    private static ActionDescriptor Clone(ActionDescriptor source)
    {
        return new ActionDescriptor
        {
            Id = source.Id,
            Description = source.Description,
            Mode = source.Mode,
            Params = source.Params.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal)
        };
    }
}
