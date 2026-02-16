using ACI.Core.Abstractions;
using ACI.Core.Models;

namespace ACI.Core.Services;

/// <summary>
/// 内存工具命名空间注册表。
/// </summary>
public sealed class ToolNamespaceRegistry : IToolNamespaceRegistry
{
    /// <summary>
    /// 命名空间映射。
    /// </summary>
    private readonly Dictionary<string, ToolNamespaceDefinition> _namespaces =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 新增或覆盖命名空间定义。
    /// </summary>
    public void Upsert(ToolNamespaceDefinition definition)
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
    public bool TryGet(string namespaceId, out ToolNamespaceDefinition? definition)
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
    /// 获取所有命名空间定义。
    /// </summary>
    public IReadOnlyList<ToolNamespaceDefinition> GetAll()
    {
        return _namespaces.Values
            .OrderBy(n => n.Id, StringComparer.OrdinalIgnoreCase)
            .Select(Clone)
            .ToList();
    }

    /// <summary>
    /// 按 ID 批量获取命名空间定义。
    /// </summary>
    public IReadOnlyList<ToolNamespaceDefinition> GetByIds(IEnumerable<string> namespaceIds)
    {
        var ids = namespaceIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var result = new List<ToolNamespaceDefinition>();
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
    /// 查找命名空间下的工具。
    /// </summary>
    public bool TryGetTool(string namespaceId, string toolId, out ToolDescriptor? tool)
    {
        tool = null;
        if (!_namespaces.TryGetValue(namespaceId, out var definition))
        {
            return false;
        }

        var matched = definition.Tools.FirstOrDefault(t =>
            string.Equals(t.Id, toolId, StringComparison.OrdinalIgnoreCase));

        if (matched == null)
        {
            return false;
        }

        tool = Clone(matched);
        return true;
    }

    /// <summary>
    /// 深拷贝命名空间定义，避免外部修改内部状态。
    /// </summary>
    private static ToolNamespaceDefinition Clone(ToolNamespaceDefinition source)
    {
        return new ToolNamespaceDefinition
        {
            Id = source.Id,
            Tools = source.Tools.Select(Clone).ToList()
        };
    }

    /// <summary>
    /// 深拷贝工具定义。
    /// </summary>
    private static ToolDescriptor Clone(ToolDescriptor source)
    {
        return new ToolDescriptor
        {
            Id = source.Id,
            Description = source.Description,
            Mode = source.Mode,
            Params = source.Params.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal)
        };
    }
}
