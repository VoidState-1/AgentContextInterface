namespace ACI.LLM;

/// <summary>
/// 解析后的单条窗口操作指令。
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
    public System.Text.Json.JsonElement? Parameters { get; set; }
}

/// <summary>
/// 一次 tool_call 中解析出的调用批次。
/// </summary>
public class ParsedActionBatch
{
    /// <summary>
    /// 调用列表（按模型输出顺序）。
    /// </summary>
    public List<ParsedAction> Calls { get; init; } = [];
}
