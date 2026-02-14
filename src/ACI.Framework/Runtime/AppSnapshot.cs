using System.Text.Json;

namespace ACI.Framework.Runtime;

/// <summary>
/// 单个 App 的快照数据模型。
/// 由 FrameworkHost 采集和恢复。
/// </summary>
public class AppSnapshot
{
    /// <summary>
    /// 应用名称（用于在恢复时匹配已注册的 App 实例）。
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 应用是否已启动。
    /// </summary>
    public bool IsStarted { get; set; }

    /// <summary>
    /// 应用管理的窗口 ID 列表。
    /// </summary>
    public List<string> ManagedWindowIds { get; set; } = [];

    /// <summary>
    /// 窗口 ID -> intent 映射（用于恢复窗口时传递 intent）。
    /// </summary>
    public Dictionary<string, string?> WindowIntents { get; set; } = [];

    /// <summary>
    /// 应用状态数据（JsonElement 字典）。
    /// </summary>
    public Dictionary<string, JsonElement> StateData { get; set; } = [];
}
