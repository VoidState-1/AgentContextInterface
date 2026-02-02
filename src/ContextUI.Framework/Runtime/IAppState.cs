namespace ContextUI.Framework.Runtime;

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
}
