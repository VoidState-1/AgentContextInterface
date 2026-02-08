namespace ACI.Server.Settings;

/// <summary>
/// ACI 运行时配置
/// </summary>
public class ACIOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "ACI";

    /// <summary>
    /// 上下文渲染裁剪配置
    /// </summary>
    public ContextRenderOptions Render { get; set; } = new();

    /// <summary>
    /// 上下文存储配置
    /// </summary>
    public ContextStorageOptions Context { get; set; } = new();
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

    /// <summary>
    /// 触发裁剪后收缩到的 Token 目标；小于等于 0 时默认收缩到 MaxTokens 的一半
    /// </summary>
    public int TrimToTokens { get; set; } = 4000;
}

public class ContextStorageOptions
{
    /// <summary>
    /// 对话项最大保留数量（仅 User/Assistant）
    /// </summary>
    public int MaxItems { get; set; } = 100;
}
