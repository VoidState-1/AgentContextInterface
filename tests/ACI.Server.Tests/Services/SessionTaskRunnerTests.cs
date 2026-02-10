using ACI.Core.Services;
using ACI.Server.Services;
using ACI.Tests.Common.Fakes;

namespace ACI.Server.Tests.Services;

public class SessionTaskRunnerTests
{
    // 测试点：启动任务后应立即返回 taskId，并按顺序发布 Started 与 Completed 事件。
    // 预期结果：返回的 taskId 可用，事件列表包含 Started/Completed。
    [Fact]
    public async Task Start_ShouldReturnTaskIdAndPublishLifecycleEvents()
    {
        var events = new SpyEventBus();
        var clock = new FakeSeqClock();
        using var runner = new SessionTaskRunner(events, clock);

        var taskId = runner.Start("win-1", _ => Task.CompletedTask, source: "test");
        await WaitForEventCountAsync(events, 2);

        Assert.StartsWith("task_", taskId);
        var lifecycle = events.PublishedEvents.OfType<BackgroundTaskLifecycleEvent>().ToList();
        Assert.Contains(lifecycle, e => e.TaskId == taskId && e.Status == BackgroundTaskStatus.Started);
        Assert.Contains(lifecycle, e => e.TaskId == taskId && e.Status == BackgroundTaskStatus.Completed);
    }

    // 测试点：相同 taskId 重复启动应被拒绝，避免状态冲突。
    // 预期结果：第二次 Start 抛出 InvalidOperationException。
    [Fact]
    public async Task Start_DuplicateTaskId_ShouldThrow()
    {
        using var gate = new ManualResetEventSlim(false);
        using var runner = new SessionTaskRunner();

        runner.Start("win-1", _ =>
        {
            gate.Wait(TimeSpan.FromSeconds(2));
            return Task.CompletedTask;
        }, taskId: "fixed-task");

        Assert.Throws<InvalidOperationException>(() =>
            runner.Start("win-1", _ => Task.CompletedTask, taskId: "fixed-task"));

        gate.Set();
        await Task.Delay(50);
    }

    // 测试点：任务抛异常时应发布 Failed 事件并携带错误信息。
    // 预期结果：事件状态为 Failed，Message 包含异常消息。
    [Fact]
    public async Task Start_WhenTaskThrows_ShouldPublishFailedEvent()
    {
        var events = new SpyEventBus();
        var clock = new FakeSeqClock();
        using var runner = new SessionTaskRunner(events, clock);

        var taskId = runner.Start("win-2", _ => throw new InvalidOperationException("boom"), source: "test");
        await WaitForEventCountAsync(events, 2);

        var failed = events.PublishedEvents
            .OfType<BackgroundTaskLifecycleEvent>()
            .FirstOrDefault(e => e.TaskId == taskId && e.Status == BackgroundTaskStatus.Failed);

        Assert.NotNull(failed);
        Assert.Contains("boom", failed!.Message);
    }

    // 测试点：取消运行中任务应返回 true，并最终发布 Canceled 事件。
    // 预期结果：Cancel 成功，生命周期包含 Canceled 状态。
    [Fact]
    public async Task Cancel_RunningTask_ShouldReturnTrueAndPublishCanceled()
    {
        var events = new SpyEventBus();
        var clock = new FakeSeqClock();
        using var runner = new SessionTaskRunner(events, clock);

        var taskId = runner.Start("win-3", async ct =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        });

        var canceled = runner.Cancel(taskId);
        await WaitForEventCountAsync(events, 2);

        Assert.True(canceled);
        Assert.Contains(events.PublishedEvents.OfType<BackgroundTaskLifecycleEvent>(),
            e => e.TaskId == taskId && e.Status == BackgroundTaskStatus.Canceled);
    }

    // 测试点：取消不存在任务时应返回 false，且不抛异常。
    // 预期结果：Cancel 返回 false。
    [Fact]
    public void Cancel_MissingTask_ShouldReturnFalse()
    {
        using var runner = new SessionTaskRunner();

        var canceled = runner.Cancel("missing-task");

        Assert.False(canceled);
    }

    private static async Task WaitForEventCountAsync(SpyEventBus bus, int expectedCount, int timeoutMs = 2000)
    {
        var start = Environment.TickCount64;
        while (bus.PublishedEvents.Count < expectedCount)
        {
            if (Environment.TickCount64 - start > timeoutMs)
            {
                throw new TimeoutException($"Expected at least {expectedCount} events, got {bus.PublishedEvents.Count}");
            }

            await Task.Delay(20);
        }
    }
}
