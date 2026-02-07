namespace ACI.LLM;

/// <summary>
/// 解析后的窗口操作指令。
/// </summary>
public class ParsedAction
{
    /// <summary>
    /// 目标窗口 ID。
    /// </summary>
    public required string WindowId { get; set; }

    /// <summary>
    /// 操作 ID。
    /// </summary>
    public required string ActionId { get; set; }

    /// <summary>
    /// 操作参数（可选）。
    /// </summary>
    public Dictionary<string, object>? Parameters { get; set; }
}
