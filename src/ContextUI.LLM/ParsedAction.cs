namespace ContextUI.LLM;

/// <summary>
/// 解析后的操作指令 - 支持 create 和 action 两种类型
/// </summary>
public class ParsedAction
{
    /// <summary>
    /// 操作类型：create, action
    /// </summary>
    public required string Type { get; set; }

    // === create 类型的字段 ===

    /// <summary>
    /// 要打开的应用名称
    /// </summary>
    public string? AppName { get; set; }

    /// <summary>
    /// AI 的意图说明
    /// </summary>
    public string? Target { get; set; }

    // === action 类型的字段 ===

    /// <summary>
    /// 目标窗口 ID
    /// </summary>
    public string? WindowId { get; set; }

    /// <summary>
    /// 操作 ID（包括 close）
    /// </summary>
    public string? ActionId { get; set; }

    /// <summary>
    /// 操作参数
    /// </summary>
    public Dictionary<string, object>? Parameters { get; set; }
}
