namespace ACI.Framework.Runtime;

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
    /// 获取该应用管理的窗口 ID 列表（供框架持久化使用）
    /// </summary>
    internal IReadOnlyList<string> GetManagedWindowIdsInternal() => _managedWindowIds;

    /// <summary>
    /// 批量恢复窗口 ID 列表（供框架恢复使用）
    /// </summary>
    internal void RestoreManagedWindowIds(IEnumerable<string> windowIds)
    {
        _managedWindowIds.Clear();
        _managedWindowIds.AddRange(windowIds);
    }

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
    /// 持久化前回调：将所有内部状态刷入 IAppState。
    /// 框架在创建快照前调用此方法。
    /// 应用应把所有不在 IAppState 中的内部字段写入 State。
    /// </summary>
    public virtual void OnSaveState() { }

    /// <summary>
    /// 恢复后回调：从 IAppState 中重建内部状态。
    /// 框架在注入 IAppState 后、重建窗口前调用此方法。
    /// 应用应从 State 中读取数据并重建内部字段。
    /// </summary>
    public virtual void OnRestoreState() { }

    /// <summary>
    /// 创建主窗口
    /// </summary>
    /// <param name="intent">用户意图（可选）</param>
    public abstract ContextWindow CreateWindow(string? intent);

    /// <summary>
    /// 刷新窗口内容（框架调用）
    /// 默认实现：重新调用 CreateWindow
    /// </summary>
    /// <param name="windowId">要刷新的窗口 ID</param>
    /// <param name="intent">原始意图</param>
    public virtual ContextWindow RefreshWindow(string windowId, string? intent = null)
    {
        return CreateWindow(intent);
    }

    // ========== 便捷方法 ==========

    /// <summary>
    /// 请求刷新指定窗口
    /// </summary>
    protected void RequestRefresh(string windowId)
    {
        Context.RequestRefresh(windowId);
    }

    /// <summary>
    /// 请求刷新所有该应用管理的窗口
    /// </summary>
    protected void RequestRefreshAll()
    {
        foreach (var id in _managedWindowIds)
        {
            Context.RequestRefresh(id);
        }
    }

    /// <summary>
    /// 启动后台任务（不阻塞当前交互）
    /// </summary>
    protected string StartBackgroundTask(
        string windowId,
        Func<CancellationToken, Task> taskBody,
        string? taskId = null)
    {
        return Context.StartBackgroundTask(windowId, taskBody, taskId);
    }

    /// <summary>
    /// 取消后台任务
    /// </summary>
    protected bool CancelBackgroundTask(string taskId)
    {
        return Context.CancelBackgroundTask(taskId);
    }

    /// <summary>
    /// 在会话串行上下文中执行动作（用于后台任务安全回写）
    /// </summary>
    protected Task RunOnSessionAsync(Func<Task> action, CancellationToken ct = default)
    {
        return Context.RunOnSessionAsync(action, ct);
    }
}
