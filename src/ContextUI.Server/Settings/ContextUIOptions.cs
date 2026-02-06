namespace ContextUI.Server.Settings;

/// <summary>
/// ContextUI 运行时配置
/// </summary>
public class ContextUIOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "ContextUI";

    /// <summary>
    /// 上下文渲染裁剪配置
    /// </summary>
    public ContextRenderOptions Render { get; set; } = new();

    /// <summary>
    /// 上下文存储配置
    /// </summary>
    public ContextStorageOptions Context { get; set; } = new();

    /// <summary>
    /// 活动日志配置
    /// </summary>
    public ActivityLogOptions ActivityLog { get; set; } = new();
}

public class ContextRenderOptions
{
    /// <summary>
    /// 上下文最大 Token 预算
    /// </summary>
    public int MaxTokens { get; set; } = 8000;

    /// <summary>
    /// 对话保留 Token 下限
    /// </summary>
    public int MinConversationTokens { get; set; } = 2000;
}

public class ContextStorageOptions
{
    /// <summary>
    /// 对话项最大保留数量（仅 User/Assistant）
    /// </summary>
    public int MaxItems { get; set; } = 100;
}

public class ActivityLogOptions
{
    /// <summary>
    /// 活动日志窗口最大数量
    /// </summary>
    public int MaxLogs { get; set; } = 50;
}
