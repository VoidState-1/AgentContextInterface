using ACI.Core.Abstractions;
using ACI.Core.Services;
using ACI.Framework.Runtime;
using ACI.Tests.Common.Fakes;

namespace ACI.Framework.Tests.Runtime;

public class RuntimeContextTests
{
    // 测试点：未配置后台任务处理器时调用 StartBackgroundTask。
    // 预期结果：抛出 InvalidOperationException，避免静默失败。
    [Fact]
    public void StartBackgroundTask_WithoutConfiguration_ShouldThrow()
    {
        var context = CreateRuntimeContext();

        Assert.Throws<InvalidOperationException>(() =>
            context.StartBackgroundTask("w-1", _ => Task.CompletedTask));
    }

    // 测试点：配置后台任务处理器后应透传 windowId/taskId 并返回处理器结果。
    // 预期结果：StartBackgroundTask 返回处理器提供的任务 ID。
    [Fact]
    public void StartBackgroundTask_WithConfiguration_ShouldReturnTaskId()
    {
        var context = CreateRuntimeContext();
        string? capturedWindowId = null;
        string? capturedTaskId = null;

        context.ConfigureBackgroundTaskHandlers(
            (windowId, _, taskId) =>
            {
                capturedWindowId = windowId;
                capturedTaskId = taskId;
                return taskId ?? "generated-task";
            },
            _ => false,
            (action, _) => action());

        var taskId = context.StartBackgroundTask("w-2", _ => Task.CompletedTask, "task-2");

        Assert.Equal("task-2", taskId);
        Assert.Equal("w-2", capturedWindowId);
        Assert.Equal("task-2", capturedTaskId);
    }

    // 测试点：CancelBackgroundTask 在处理器存在时应返回处理器判定结果。
    // 预期结果：可取消 ID 返回 true，不可取消 ID 返回 false。
    [Fact]
    public void CancelBackgroundTask_ShouldFollowConfiguredHandler()
    {
        var context = CreateRuntimeContext();

        context.ConfigureBackgroundTaskHandlers(
            (_, _, taskId) => taskId ?? "generated-task",
            taskId => taskId == "ok-task",
            (action, _) => action());

        var ok = context.CancelBackgroundTask("ok-task");
        var failed = context.CancelBackgroundTask("bad-task");

        Assert.True(ok);
        Assert.False(failed);
    }

    // 测试点：RunOnSessionAsync 在配置后应通过会话处理器执行动作。
    // 预期结果：处理器被调用且动作体被执行。
    [Fact]
    public async Task RunOnSessionAsync_ShouldUseConfiguredDispatcher()
    {
        var context = CreateRuntimeContext();
        var dispatcherCalled = false;
        var actionCalled = false;

        context.ConfigureBackgroundTaskHandlers(
            (_, _, taskId) => taskId ?? "generated-task",
            _ => false,
            async (action, _) =>
            {
                dispatcherCalled = true;
                await action();
            });

        await context.RunOnSessionAsync(() =>
        {
            actionCalled = true;
            return Task.CompletedTask;
        });

        Assert.True(dispatcherCalled);
        Assert.True(actionCalled);
    }

    // 测试点：GetService 应从注入的 IServiceProvider 获取指定类型实例。
    // 预期结果：已注册服务可取回，未注册服务返回 null。
    [Fact]
    public void GetService_ShouldResolveFromServiceProvider()
    {
        var provider = new DictionaryServiceProvider(new Dictionary<Type, object>
        {
            [typeof(TestService)] = new TestService { Name = "ACI" }
        });
        var context = CreateRuntimeContext(provider);

        var service = context.GetService<TestService>();
        var missing = context.GetService<RuntimeContextTests>();

        Assert.NotNull(service);
        Assert.Equal("ACI", service!.Name);
        Assert.Null(missing);
    }

    private static RuntimeContext CreateRuntimeContext(IServiceProvider? provider = null)
    {
        var clock = new FakeSeqClock();
        var windows = new WindowManager(clock);
        var events = new SpyEventBus();
        var manager = new ContextManager(clock);
        return new RuntimeContext(windows, events, clock, manager,
            AgentProfile.Default(), new LocalMessageChannel("test"), provider);
    }

    private sealed class TestService
    {
        public required string Name { get; init; }
    }

    private sealed class DictionaryServiceProvider : IServiceProvider
    {
        private readonly IReadOnlyDictionary<Type, object> _services;

        public DictionaryServiceProvider(IReadOnlyDictionary<Type, object> services)
        {
            _services = services;
        }

        public object? GetService(Type serviceType)
        {
            return _services.TryGetValue(serviceType, out var service)
                ? service
                : null;
        }
    }
}
