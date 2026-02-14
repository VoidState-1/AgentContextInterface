using System.Text.Json;

namespace ACI.Framework.Runtime;

/// <summary>
/// 应用状态存储接口
/// </summary>
public interface IAppState
{
    /// <summary>
    /// 保存状态
    /// </summary>
    void Set<T>(string key, T value);

    /// <summary>
    /// 读取状态
    /// </summary>
    T? Get<T>(string key);

    /// <summary>
    /// 检查是否存在
    /// </summary>
    bool Has(string key);

    /// <summary>
    /// 清除所有状态
    /// </summary>
    void Clear();

    /// <summary>
    /// 导出所有状态为可序列化的字典（用于持久化）。
    /// </summary>
    IReadOnlyDictionary<string, JsonElement> Export();

    /// <summary>
    /// 从可序列化字典导入状态（用于恢复）。
    /// 调用后覆盖现有数据。
    /// </summary>
    void Import(IReadOnlyDictionary<string, JsonElement> data);
}
