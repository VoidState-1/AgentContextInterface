using ACI.Core.Abstractions;
using ACI.Core.Models;
using ACI.Core.Services;
using ACI.Framework.Components;
using ACI.Framework.Runtime;
using ACI.Tests.Common.Fakes;
using System.Text.Json;

namespace ACI.Framework.Tests.Runtime;

public class FrameworkHostTests
{
    // 测试点：Start 应只在首次启动时调用应用生命周期初始化。
    // 预期结果：同一应用重复 Start 时 OnCreate 只执行一次。
    [Fact]
    public void Start_ShouldInitializeAppOnlyOnce()
    {
        var runtime = CreateRuntimeContext();
        var host = new FrameworkHost(runtime);
        var app = new TestApp("demo");
        host.Register(app);

        host.Start("demo");
        host.Start("demo");

        Assert.True(host.IsStarted("demo"));
        Assert.Equal(1, app.OnCreateCalls);
    }

    // 测试点：Launch 应创建窗口、写入 appName，并发布 AppCreatedEvent。
    // 预期结果：窗口进入 WindowManager，事件中包含 appName 与 target。
    [Fact]
    public void Launch_ShouldCreateWindowAndPublishAppCreatedEvent()
    {
        var clock = new FakeSeqClock(seed: 10);
        var windows = new WindowManager(clock);
        var events = new SpyEventBus();
        var context = new RuntimeContext(windows, events, clock, new ContextManager(clock),
            AgentProfile.Default(), new LocalMessageChannel("test"));
        var host = new FrameworkHost(context);
        var app = new TestApp("demo");
        host.Register(app);

        var window = host.Launch("demo", intent: "search");

        var appCreated = Assert.Single(events.PublishedEvents.OfType<AppCreatedEvent>());
        Assert.Equal("demo", window.AppName);
        Assert.NotNull(windows.Get(window.Id));
        Assert.Equal("demo", appCreated.AppName);
        Assert.Equal("search", appCreated.Target);
    }

    // 测试点：Launch 未注册应用时应快速失败。
    // 预期结果：抛出 InvalidOperationException。
    [Fact]
    public void Launch_WithUnknownApp_ShouldThrow()
    {
        var runtime = CreateRuntimeContext();
        var host = new FrameworkHost(runtime);

        Assert.Throws<InvalidOperationException>(() => host.Launch("missing"));
    }

    // 测试点：RefreshWindow 应原地更新窗口内容并保留 CreatedAt。
    // 预期结果：CreatedAt 不变、UpdatedAt 前进、内容和动作定义更新。
    [Fact]
    public void RefreshWindow_ShouldUpdateWindowInPlace()
    {
        var clock = new FakeSeqClock(seed: 100);
        var windows = new WindowManager(clock);
        var events = new SpyEventBus();
        var context = new RuntimeContext(windows, events, clock, new ContextManager(clock),
            AgentProfile.Default(), new LocalMessageChannel("test"));
        var host = new FrameworkHost(context);
        var app = new TestApp("demo");
        host.Register(app);

        var launched = host.Launch("demo", intent: "init");
        var createdAt = launched.Meta.CreatedAt;
        var beforeContent = launched.Content.Render();
        var beforeActionId = launched.Actions[0].Id;

        host.RefreshWindow(launched.Id);

        var refreshed = windows.Get(launched.Id);
        Assert.NotNull(refreshed);
        Assert.Equal(createdAt, refreshed!.Meta.CreatedAt);
        Assert.True(refreshed.Meta.UpdatedAt > createdAt);
        Assert.NotEqual(beforeContent, refreshed.Content.Render());
        Assert.NotEqual(beforeActionId, refreshed.Actions[0].Id);
        Assert.Equal(1, app.RefreshCalls);
        Assert.Single(events.PublishedEvents.OfType<WindowRefreshedEvent>());
    }

    // 测试点：RequestRefresh 通过 RuntimeContext 调用时应触发 Host 刷新逻辑。
    // 预期结果：应用 RefreshWindow 被调用，窗口内容版本提升。
    [Fact]
    public void RequestRefresh_ShouldInvokeHostRefreshHandler()
    {
        var runtime = CreateRuntimeContext();
        var host = new FrameworkHost(runtime);
        var app = new TestApp("demo");
        host.Register(app);

        var launched = host.Launch("demo");
        var beforeContent = launched.Content.Render();

        runtime.RequestRefresh(launched.Id);

        var refreshed = runtime.Windows.Get(launched.Id);
        Assert.NotNull(refreshed);
        Assert.NotEqual(beforeContent, refreshed!.Content.Render());
        Assert.Equal(1, app.RefreshCalls);
    }

    // 测试点：Close 应销毁应用并移除其管理的窗口。
    // 预期结果：窗口被删除、OnDestroy 调用、应用状态变为未启动。
    [Fact]
    public void Close_ShouldDestroyAppAndRemoveManagedWindows()
    {
        var runtime = CreateRuntimeContext();
        var host = new FrameworkHost(runtime);
        var app = new TestApp("demo");
        host.Register(app);

        var launched = host.Launch("demo");
        host.Close("demo");

        Assert.Null(runtime.Windows.Get(launched.Id));
        Assert.Equal(1, app.OnDestroyCalls);
        Assert.False(host.IsStarted("demo"));
    }

    // 测试点：恢复过程中单个 App 抛异常时，不应阻断其他 App 恢复。
    // 预期结果：异常 App 被跳过，健康 App 仍完成 OnRestoreState。
    [Fact]
    public void RestoreAppSnapshots_WhenOneAppThrows_ShouldContinueRestoringOthers()
    {
        var runtime = CreateRuntimeContext();
        var host = new FrameworkHost(runtime);
        var brokenApp = new ThrowOnRestoreApp("broken");
        var healthyApp = new RestoreFlagApp("healthy");
        host.Register(brokenApp);
        host.Register(healthyApp);

        var snapshots = new List<AppSnapshot>
        {
            new()
            {
                Name = "broken",
                IsStarted = true,
                ManagedWindowIds = [],
                WindowIntents = new Dictionary<string, string?>(),
                StateData = new Dictionary<string, JsonElement>()
            },
            new()
            {
                Name = "healthy",
                IsStarted = true,
                ManagedWindowIds = [],
                WindowIntents = new Dictionary<string, string?>(),
                StateData = new Dictionary<string, JsonElement>()
            }
        };

        var ex = Record.Exception(() => host.RestoreAppSnapshots(snapshots));

        Assert.Null(ex);
        Assert.True(host.IsStarted("healthy"));
        Assert.True(healthyApp.Restored);
    }

    private static RuntimeContext CreateRuntimeContext()
    {
        var clock = new FakeSeqClock();
        var windows = new WindowManager(clock);
        var events = new SpyEventBus();
        return new RuntimeContext(windows, events, clock, new ContextManager(clock),
            AgentProfile.Default(), new LocalMessageChannel("test"));
    }

    private sealed class TestApp : ContextApp
    {
        private int _version = 1;
        private readonly string _name;

        public TestApp(string name)
        {
            _name = name;
        }

        public override string Name => _name;
        public override string? AppDescription => "test app";

        public int OnCreateCalls { get; private set; }
        public int OnDestroyCalls { get; private set; }
        public int RefreshCalls { get; private set; }

        public override void OnCreate()
        {
            OnCreateCalls++;
        }

        public override void OnDestroy()
        {
            OnDestroyCalls++;
        }

        public override ContextWindow CreateWindow(string? intent)
        {
            const string windowId = "demo_window";
            RegisterWindow(windowId);
            return BuildWindow(windowId, intent, _version);
        }

        public override ContextWindow RefreshWindow(string windowId, string? intent = null)
        {
            RefreshCalls++;
            _version++;
            return BuildWindow(windowId, intent, _version);
        }

        private static ContextWindow BuildWindow(string windowId, string? intent, int version)
        {
            return new ContextWindow
            {
                Id = windowId,
                Description = new Text($"intent:{intent ?? "none"}"),
                Content = new Text($"version:{version}"),
                Actions =
                [
                    new ContextAction
                    {
                        Id = $"run_v{version}",
                        Label = "Run",
                        Handler = _ => Task.FromResult(ActionResult.Ok())
                    }
                ]
            };
        }
    }

    private sealed class ThrowOnRestoreApp : ContextApp
    {
        private readonly string _name;

        public ThrowOnRestoreApp(string name)
        {
            _name = name;
        }

        public override string Name => _name;

        public override void OnRestoreState()
        {
            throw new InvalidOperationException("restore failed");
        }

        public override ContextWindow CreateWindow(string? intent)
        {
            const string windowId = "broken_window";
            RegisterWindow(windowId);
            return new ContextWindow
            {
                Id = windowId,
                Content = new Text("broken")
            };
        }
    }

    private sealed class RestoreFlagApp : ContextApp
    {
        private readonly string _name;

        public RestoreFlagApp(string name)
        {
            _name = name;
        }

        public override string Name => _name;

        public bool Restored { get; private set; }

        public override void OnRestoreState()
        {
            Restored = true;
        }

        public override ContextWindow CreateWindow(string? intent)
        {
            const string windowId = "healthy_window";
            RegisterWindow(windowId);
            return new ContextWindow
            {
                Id = windowId,
                Content = new Text("healthy")
            };
        }
    }
}
