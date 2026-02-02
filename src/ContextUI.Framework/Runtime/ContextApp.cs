namespace ContextUI.Framework.Runtime;

/// <summary>
/// 应用基类
/// </summary>
public abstract class ContextApp
{
    /// <summary>
    /// 应用唯一标识
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// 应用描述（给 AppLauncher 展示）
    /// </summary>
    public virtual string? AppDescription => null;

    /// <summary>
    /// 应用标签（用于分类/搜索）
    /// </summary>
    public virtual string[] Tags => [];

    /// <summary>
    /// 应用状态（在 Initialize 之后可用）
    /// </summary>
    protected IAppState State { get; private set; } = null!;

    /// <summary>
    /// 运行时上下文（在 OnCreate 时注入）
    /// </summary>
    protected IContext Context { get; private set; } = null!;

    /// <summary>
    /// 该应用管理的窗口列表
    /// </summary>
    private readonly List<string> _managedWindowIds = [];

    /// <summary>
    /// 内部初始化（框架调用）
    /// </summary>
    internal void Initialize(IAppState state, IContext context)
    {
        State = state;
        Context = context;
    }

    /// <summary>
    /// 获取该应用管理的所有窗口 ID
    /// </summary>
    public IReadOnlyList<string> ManagedWindowIds => _managedWindowIds;

    /// <summary>
    /// 注册窗口（应用调用）
    /// </summary>
    protected void RegisterWindow(string windowId)
    {
        if (!_managedWindowIds.Contains(windowId))
        {
            _managedWindowIds.Add(windowId);
        }
    }

    /// <summary>
    /// 取消注册窗口
    /// </summary>
    protected void UnregisterWindow(string windowId)
    {
        _managedWindowIds.Remove(windowId);
    }

    // ========== 生命周期 ==========

    /// <summary>
    /// 应用创建时调用
    /// </summary>
    public virtual void OnCreate() { }

    /// <summary>
    /// 应用销毁时调用
    /// </summary>
    public virtual void OnDestroy() { }

    /// <summary>
    /// 创建主窗口
    /// </summary>
    /// <param name="intent">用户意图（可选）</param>
    public abstract ContextWindow CreateWindow(string? intent);
}
