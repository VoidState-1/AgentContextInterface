using ACI.Core.Models;
using ACI.Core.Services;
using ACI.Framework.Components;
using ACI.Framework.Runtime;
using ACI.Tests.Common.Fakes;

namespace ACI.Framework.Tests.Runtime;

/// <summary>
/// 测试 App 持久化生命周期：OnSaveState / OnRestoreState。
/// </summary>
public class AppSaveRestoreTests
{
    // 测试点：OnSaveState 应将 App 内部字段刷入 IAppState。
    [Fact]
    public void OnSaveState_ShouldFlushInternalFieldsToAppState()
    {
        var runtime = CreateRuntimeContext();
        var host = new FrameworkHost(runtime);
        var app = new StatefulTestApp("stateful");
        host.Register(app);
        host.Start("stateful");

        // 修改内部状态
        app.Counter = 5;
        app.Labels.AddRange(["a", "b", "c"]);

        // 调用 OnSaveState
        app.OnSaveState();

        // 验证 State 中有正确的值
        Assert.Equal(5, app.ExposedState.Get<int>("counter"));
        var labels = app.ExposedState.Get<List<string>>("labels");
        Assert.NotNull(labels);
        Assert.Equal(3, labels.Count);
        Assert.Equal("b", labels[1]);
    }

    // 测试点：OnRestoreState 应从 IAppState 重建内部字段。
    [Fact]
    public void OnRestoreState_ShouldRebuildInternalFieldsFromAppState()
    {
        var runtime = CreateRuntimeContext();
        var host = new FrameworkHost(runtime);
        var app = new StatefulTestApp("stateful");
        host.Register(app);
        host.Start("stateful");

        // 模拟已有状态
        app.ExposedState.Set("counter", 10);
        app.ExposedState.Set("labels", new List<string> { "x", "y" });

        // 调用 OnRestoreState
        app.OnRestoreState();

        Assert.Equal(10, app.Counter);
        Assert.Equal(2, app.Labels.Count);
        Assert.Equal("x", app.Labels[0]);
    }

    // 测试点：完整的 Save -> Export -> Import -> Restore 往返测试。
    [Fact]
    public void FullRoundTrip_SaveExportImportRestore_ShouldPreserveState()
    {
        var runtime = CreateRuntimeContext();
        var host = new FrameworkHost(runtime);
        var app = new StatefulTestApp("stateful");
        host.Register(app);
        host.Start("stateful");

        // 设置内部状态
        app.Counter = 42;
        app.Labels.AddRange(["alpha", "beta"]);

        // Save → Export
        app.OnSaveState();
        var exported = app.ExposedState.Export();

        // 模拟创建一个新的 App 实例（恢复场景）
        var runtime2 = CreateRuntimeContext();
        var host2 = new FrameworkHost(runtime2);
        var app2 = new StatefulTestApp("stateful");
        host2.Register(app2);
        host2.Start("stateful");

        // Import → Restore
        app2.ExposedState.Import(exported);
        app2.OnRestoreState();

        Assert.Equal(42, app2.Counter);
        Assert.Equal(2, app2.Labels.Count);
        Assert.Equal("alpha", app2.Labels[0]);
        Assert.Equal("beta", app2.Labels[1]);
    }

    // 测试点：OnRestoreState 缺少某些键时应安全降级。
    [Fact]
    public void OnRestoreState_WithMissingKeys_ShouldUseDefaults()
    {
        var runtime = CreateRuntimeContext();
        var host = new FrameworkHost(runtime);
        var app = new StatefulTestApp("stateful");
        host.Register(app);
        host.Start("stateful");

        // 不设置任何状态直接调用 OnRestoreState
        app.OnRestoreState();

        Assert.Equal(0, app.Counter);
        Assert.Empty(app.Labels);
    }

    // 测试点：GetManagedWindowIdsInternal / RestoreManagedWindowIds 应正确工作。
    [Fact]
    public void ManagedWindowIds_SaveAndRestore_ShouldWork()
    {
        var runtime = CreateRuntimeContext();
        var host = new FrameworkHost(runtime);
        var app = new StatefulTestApp("stateful");
        host.Register(app);

        var window = host.Launch("stateful");

        // 验证 GetManagedWindowIdsInternal 返回已注册的窗口
        var ids = app.GetManagedWindowIdsInternal();
        Assert.Contains("stateful_window", ids);

        // 模拟恢复
        var runtime2 = CreateRuntimeContext();
        var host2 = new FrameworkHost(runtime2);
        var app2 = new StatefulTestApp("stateful");
        host2.Register(app2);
        host2.Start("stateful");

        app2.RestoreManagedWindowIds(ids);
        var restoredIds = app2.GetManagedWindowIdsInternal();

        Assert.Equal(ids.Count, restoredIds.Count);
        Assert.Contains("stateful_window", restoredIds);
    }

    private static RuntimeContext CreateRuntimeContext()
    {
        var clock = new FakeSeqClock();
        var windows = new WindowManager(clock);
        var events = new SpyEventBus();
        return new RuntimeContext(windows, events, clock, new ContextManager(clock),
            new ActionNamespaceRegistry(), AgentProfile.Default(), new LocalMessageChannel("test"));
    }

    /// <summary>
    /// 有内部状态的测试 App。
    /// </summary>
    private sealed class StatefulTestApp : ContextApp
    {
        private readonly string _name;

        public StatefulTestApp(string name) { _name = name; }

        public override string Name => _name;

        // 暴露 State 供测试直接读写
        public IAppState ExposedState => State;

        // 内部状态（不在 IAppState 中）
        public int Counter { get; set; }
        public List<string> Labels { get; } = [];

        public override void OnSaveState()
        {
            State.Set("counter", Counter);
            State.Set("labels", Labels.ToList());
        }

        public override void OnRestoreState()
        {
            Counter = State.Get<int>("counter");
            Labels.Clear();
            var saved = State.Get<List<string>>("labels");
            if (saved != null) Labels.AddRange(saved);
        }

        public override ContextWindow CreateWindow(string? intent)
        {
            const string windowId = "stateful_window";
            RegisterWindow(windowId);
            return new ContextWindow
            {
                Id = windowId,
                Content = new Text($"Counter: {Counter}, Labels: {string.Join(",", Labels)}"),
                Actions =
                [
                    new ContextAction
                    {
                        Id = "noop",
                        Label = "Do Nothing",
                        Handler = _ => Task.FromResult(ActionResult.Ok())
                    }
                ]
            };
        }
    }
}
