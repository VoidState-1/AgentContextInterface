namespace ContextUI.Core.Models;

/// <summary>
/// 操作执行结果（统一的结果类型）
/// </summary>
public sealed class ActionResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 临时消息（显示给用户）
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// 持久摘要（记录到日志）
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// 是否需要刷新窗口内容
    /// </summary>
    public bool ShouldRefresh { get; init; }

    /// <summary>
    /// 是否应该关闭窗口
    /// </summary>
    public bool ShouldClose { get; init; }

    /// <summary>
    /// 附加数据
    /// </summary>
    public object? Data { get; init; }

    /// <summary>
    /// 关联的日志序列号（执行后填充）
    /// </summary>
    public int LogSeq { get; set; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static ActionResult Ok(
        string? message = null,
        string? summary = null,
        bool shouldRefresh = true,
        bool shouldClose = false,
        object? data = null)
        => new()
        {
            Success = true,
            Message = message,
            Summary = summary,
            ShouldRefresh = shouldRefresh,
            ShouldClose = shouldClose,
            Data = data
        };

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static ActionResult Fail(string message)
        => new()
        {
            Success = false,
            Message = message,
            ShouldRefresh = false,
            ShouldClose = false
        };

    /// <summary>
    /// 创建关闭窗口的结果
    /// </summary>
    public static ActionResult Close(string? summary = null)
        => new()
        {
            Success = true,
            Summary = summary,
            ShouldRefresh = false,
            ShouldClose = true
        };
}
