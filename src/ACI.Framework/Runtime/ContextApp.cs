using ACI.Core.Models;

namespace ACI.Framework.Runtime;

/// <summary>
/// 应用基类。
/// </summary>
public abstract class ContextApp
{
    /// <summary>
    /// 应用唯一标识。
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// 应用描述（给 AppLauncher 展示）。
    /// </summary>
    public virtual string? AppDescription => null;

    /// <summary>
    /// 应用标签（用于分类/搜索）。
    /// </summary>
    public virtual string[] Tags => [];

    /// <summary>
    /// 应用状态（在 Initialize 之后可用）。
    /// </summary>
    protected IAppState State { get; private set; } = null!;

    /// <summary>
    /// 运行时上下文（在 OnCreate 时注入）。
    /// </summary>
    protected IContext Context { get; private set; } = null!;

    /// <summary>
    /// 该应用管理的窗口列表。
    /// </summary>
    private readonly List<string> _managedWindowIds = [];

    /// <summary>
    /// 应用注册的 Action 命名空间与动作定义。
    /// </summary>
    private readonly Dictionary<string, List<ContextAction>> _namespaceActions =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 获取该应用管理的窗口 ID 列表（供框架持久化使用）。
    /// </summary>
    internal IReadOnlyList<string> GetManagedWindowIdsInternal() => _managedWindowIds;

    /// <summary>
    /// 批量恢复窗口 ID 列表（供框架恢复使用）。
    /// </summary>
    internal void RestoreManagedWindowIds(IEnumerable<string> windowIds)
    {
        _managedWindowIds.Clear();
        _managedWindowIds.AddRange(windowIds);
    }

    /// <summary>
    /// 内部初始化（框架调用）。
    /// </summary>
    internal void Initialize(IAppState state, IContext context)
    {
        State = state;
        Context = context;
    }

    /// <summary>
    /// 获取该应用管理的所有窗口 ID。
    /// </summary>
    public IReadOnlyList<string> ManagedWindowIds => _managedWindowIds;

    /// <summary>
    /// 注册窗口（应用调用）。
    /// </summary>
    protected void RegisterWindow(string windowId)
    {
        if (!_managedWindowIds.Contains(windowId))
        {
            _managedWindowIds.Add(windowId);
        }
    }

    /// <summary>
    /// 取消注册窗口。
    /// </summary>
    protected void UnregisterWindow(string windowId)
    {
        _managedWindowIds.Remove(windowId);
    }

    // ========== 生命周期 ==========

    /// <summary>
    /// 应用创建时调用。
    /// </summary>
    public virtual void OnCreate() { }

    /// <summary>
    /// 应用销毁时调用。
    /// </summary>
    public virtual void OnDestroy() { }

    /// <summary>
    /// 持久化前回调。
    /// </summary>
    public virtual void OnSaveState() { }

    /// <summary>
    /// 恢复后回调。
    /// </summary>
    public virtual void OnRestoreState() { }

    /// <summary>
    /// 创建主窗口。
    /// </summary>
    public abstract ContextWindow CreateWindow(string? intent);

    /// <summary>
    /// 刷新窗口内容（框架调用）。
    /// </summary>
    public virtual ContextWindow RefreshWindow(string windowId, string? intent = null)
    {
        return CreateWindow(intent);
    }

    // ========== 便捷方法 ==========

    /// <summary>
    /// 请求刷新指定窗口。
    /// </summary>
    protected void RequestRefresh(string windowId)
    {
        Context.RequestRefresh(windowId);
    }

    /// <summary>
    /// 注册 Action 命名空间。
    /// 一次注册同时产出：
    /// 1) 运行时执行处理器（ContextAction）；
    /// 2) 提示词渲染元数据（ActionDescriptor）。
    /// </summary>
    protected void RegisterActionNamespace(string namespaceId, IEnumerable<ContextAction> actions)
    {
        if (string.IsNullOrWhiteSpace(namespaceId))
        {
            throw new ArgumentException("Namespace id cannot be empty.", nameof(namespaceId));
        }

        ArgumentNullException.ThrowIfNull(actions);

        var actionList = actions.ToList();
        var duplicated = actionList
            .GroupBy(a => a.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicated != null)
        {
            throw new InvalidOperationException(
                $"Duplicate action id '{duplicated.Key}' in namespace '{namespaceId}'.");
        }

        _namespaceActions[namespaceId] = actionList.Select(CloneAction).ToList();

        var descriptors = actionList
            .Select(ToDescriptor)
            .ToList();

        Context.ActionNamespaces.Upsert(new ActionNamespaceDefinition
        {
            Id = namespaceId,
            Actions = descriptors
        });
    }

    /// <summary>
    /// 按窗口可见命名空间收集 Action 执行定义。
    /// </summary>
    protected List<ContextAction> ResolveRegisteredActions(IEnumerable<string> namespaceRefs)
    {
        var resolved = new List<ContextAction>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var ns in namespaceRefs.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            if (!_namespaceActions.TryGetValue(ns, out var actions))
            {
                continue;
            }

            foreach (var action in actions)
            {
                var key = $"{ns}.{action.Id}";
                if (!seen.Add(key))
                {
                    continue;
                }

                resolved.Add(CloneAction(action));
            }
        }

        return resolved;
    }

    /// <summary>
    /// 请求刷新所有该应用管理的窗口。
    /// </summary>
    protected void RequestRefreshAll()
    {
        foreach (var id in _managedWindowIds)
        {
            Context.RequestRefresh(id);
        }
    }

    /// <summary>
    /// 启动后台任务（不阻塞当前交互）。
    /// </summary>
    protected string StartBackgroundTask(
        string windowId,
        Func<CancellationToken, Task> taskBody,
        string? taskId = null)
    {
        return Context.StartBackgroundTask(windowId, taskBody, taskId);
    }

    /// <summary>
    /// 取消后台任务。
    /// </summary>
    protected bool CancelBackgroundTask(string taskId)
    {
        return Context.CancelBackgroundTask(taskId);
    }

    /// <summary>
    /// 在会话串行上下文中执行动作（用于后台任务安全回写）。
    /// </summary>
    protected Task RunOnSessionAsync(Func<Task> action, CancellationToken ct = default)
    {
        return Context.RunOnSessionAsync(action, ct);
    }

    /// <summary>
    /// 克隆 Action 定义，避免引用外部可变集合。
    /// </summary>
    private static ContextAction CloneAction(ContextAction action)
    {
        return new ContextAction
        {
            Id = action.Id,
            Description = action.Description,
            Handler = action.Handler,
            Params = action.Params,
            Mode = action.Mode
        };
    }

    /// <summary>
    /// 将运行时 Action 定义投影为渲染用元数据。
    /// </summary>
    private static ActionDescriptor ToDescriptor(ContextAction action)
    {
        return new ActionDescriptor
        {
            Id = action.Id,
            Description = action.Description,
            Mode = action.Mode,
            Params = BuildPromptParams(action.Params)
        };
    }

    /// <summary>
    /// 将参数结构压缩为提示词签名映射。
    /// </summary>
    private static Dictionary<string, string> BuildPromptParams(ActionParamSchema? schema)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (schema == null)
        {
            return result;
        }

        if (schema.Kind == ActionParamKind.Object && schema.Properties != null)
        {
            foreach (var (name, propertySchema) in schema.Properties)
            {
                var signature = propertySchema.ToPromptSignature();
                if (!propertySchema.Required)
                {
                    signature += "?";
                }

                result[name] = signature;
            }

            return result;
        }

        var valueSignature = schema.ToPromptSignature();
        if (!schema.Required)
        {
            valueSignature += "?";
        }

        result["value"] = valueSignature;
        return result;
    }
}
