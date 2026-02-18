using ACI.Core.Abstractions;
using ACI.Core.Models;
using ACI.Core.Services;
using ACI.Framework.Components;
using ACI.Framework.Runtime;
using ACI.LLM.Abstractions;
using ACI.Tests.Common.Fakes;

namespace ACI.LLM.Tests;

public class InteractionControllerTests
{
    // 测试点：当模型先返回 action_call 再返回普通文本时，系统应自动继续循环。
    // 预期结果：LLM 被调用两次，最终响应为普通文本，且产出 1 条步骤记录。
    [Fact]
    public async Task ProcessAsync_ToolCallThenText_ShouldAutoLoopUntilText()
    {
        var llm = new QueueLlmBridge(
        [
            LLMResponse.Ok("""
                           <action_call>
                           {"calls":[{"window_id":"demo_window","action_id":"demo.sync_echo","params":{"text":"hello"}}]}
                           </action_call>
                           """),
            LLMResponse.Ok("final assistant response")
        ]);

        var sut = CreateController(llm);

        var result = await sut.ProcessAsync("run");

        Assert.True(result.Success);
        Assert.Equal("final assistant response", result.Response);
        Assert.NotNull(result.Action);
        Assert.Equal("demo.sync_echo", result.Action!.ActionId);
        Assert.NotNull(result.ActionResult);
        Assert.True(result.ActionResult!.Success);
        Assert.NotNull(result.Steps);
        Assert.Single(result.Steps!);
        Assert.Equal(2, llm.CallCount);
    }

    // 测试点：连续超过最大 action_call 轮次时必须安全终止。
    // 预期结果：返回失败结果，错误信息包含 consecutive action_call turns。
    [Fact]
    public async Task ProcessAsync_TooManyToolCallTurns_ShouldFailSafely()
    {
        var llm = new ConstantLlmBridge(
            LLMResponse.Ok("""
                           <action_call>
                           {"calls":[{"window_id":"demo_window","action_id":"demo.sync_echo","params":{"text":"loop"}}]}
                           </action_call>
                           """));
        var sut = CreateController(llm);

        var result = await sut.ProcessAsync("start loop");

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("consecutive action_call turns", result.Error);
    }

    // 测试点：LLM 调用失败时应直接返回失败，不进入后续工具执行。
    // 预期结果：InteractionResult.Success 为 false，Error 保留原始失败信息。
    [Fact]
    public async Task ProcessAsync_WhenLlmFails_ShouldReturnFailure()
    {
        var llm = new QueueLlmBridge([LLMResponse.Fail("llm unavailable")]);
        var sut = CreateController(llm);

        var result = await sut.ProcessAsync("hi");

        Assert.False(result.Success);
        Assert.Equal("llm unavailable", result.Error);
    }

    // 测试点：assistant 输出中含 action_call 时，应执行动作并返回步骤列表。
    // 预期结果：步骤数量为 1，步骤携带 callId 与成功状态。
    [Fact]
    public async Task ProcessAssistantOutputAsync_WithToolCall_ShouldExecuteAndReturnSteps()
    {
        var llm = new QueueLlmBridge([]);
        var sut = CreateController(llm);
        var assistantOutput = """
                              <action_call>
                              {"calls":[{"window_id":"demo_window","action_id":"demo.sync_echo","params":{"text":"from-assistant"}}]}
                              </action_call>
                              """;

        var result = await sut.ProcessAssistantOutputAsync(assistantOutput);

        Assert.True(result.Success);
        Assert.Equal(assistantOutput, result.Response);
        Assert.NotNull(result.Steps);
        var step = Assert.Single(result.Steps!);
        Assert.Equal("call_1_1", step.CallId);
        Assert.Equal("sync", step.ResolvedMode);
        Assert.True(step.Success);
    }

    // 测试点：异步动作应由 startBackgroundTask 分支处理，并在步骤中返回 task_id。
    // 预期结果：ResolvedMode 为 async，TaskId 等于分配值，且不阻塞主流程。
    [Fact]
    public async Task ProcessAssistantOutputAsync_WithAsyncAction_ShouldReturnAsyncStep()
    {
        var llm = new QueueLlmBridge([]);
        var started = false;
        Func<string, Func<CancellationToken, Task>, string?, string> startTask = (windowId, _, taskId) =>
        {
            started = true;
            return taskId ?? "task_async_1";
        };

        var sut = CreateController(llm, startBackgroundTask: startTask);
        var assistantOutput = """
                              <action_call>
                              {"calls":[{"window_id":"demo_window","action_id":"demo.async_job"}]}
                              </action_call>
                              """;

        var result = await sut.ProcessAssistantOutputAsync(assistantOutput);

        Assert.True(started);
        Assert.True(result.Success);
        Assert.NotNull(result.Steps);
        var step = Assert.Single(result.Steps!);
        Assert.Equal("async", step.ResolvedMode);
        Assert.Equal("task_async_1", step.TaskId);
        Assert.True(step.Success);
    }

    // 测试点：GetCurrentLlmInputSnapshot 应包含初始化后的 system prompt。
    // 预期结果：消息列表中至少包含 1 条 system 角色消息。
    [Fact]
    public void GetCurrentLlmInputSnapshot_ShouldContainSystemPrompt()
    {
        var sut = CreateController(new QueueLlmBridge([]));

        var messages = sut.GetCurrentLlmInputSnapshot();

        Assert.Contains(messages, m => m.Role == "system");
    }

    private static InteractionController CreateController(
        ILLMBridge llm,
        Func<string, Func<CancellationToken, Task>, string?, string>? startBackgroundTask = null)
    {
        var clock = new FakeSeqClock(seed: 100);
        var events = new SpyEventBus();
        var windows = new WindowManager(clock);
        var context = new ContextManager(clock);
        var runtime = new RuntimeContext(windows, events, clock, context,
            new ActionNamespaceRegistry(), AgentProfile.Default(), new LocalMessageChannel("test"));
        var host = new FrameworkHost(runtime);
        host.Register(new DemoInteractionApp());
        host.Launch("demo_app");

        var actionExecutor = new ActionExecutor(windows, clock, events, host.RefreshWindow);
        return new InteractionController(
            llm,
            host,
            context,
            windows,
            runtime.ActionNamespaces,
            actionExecutor,
            renderOptions: new RenderOptions
            {
                MaxTokens = 4000,
                MinConversationTokens = 800,
                PruneTargetTokens = 2000
            },
            startBackgroundTask: startBackgroundTask);
    }

    private sealed class DemoInteractionApp : ContextApp
    {
        public override string Name => "demo_app";

        public override void OnCreate()
        {
            RegisterActionNamespace("demo",
            [
                new ContextAction
                {
                    Id = "sync_echo",
                    Description = "Echo the input text.",
                    Params = Param.Object(new Dictionary<string, ActionParamSchema>
                    {
                        ["text"] = Param.String()
                    }),
                    Handler = ctx =>
                    {
                        var text = ctx.GetString("text");
                        return Task.FromResult(ActionResult.Ok(
                            message: text,
                            summary: $"echo:{text}",
                            shouldRefresh: false));
                    }
                },
                new ContextAction
                {
                    Id = "async_job",
                    Description = "Run an async job.",
                    Handler = _ => Task.FromResult(ActionResult.Ok(
                        message: "async done",
                        shouldRefresh: false))
                }.AsAsync()
            ]);
        }

        public override ContextWindow CreateWindow(string? intent)
        {
            const string windowId = "demo_window";
            RegisterWindow(windowId);

            return new ContextWindow
            {
                Id = windowId,
                Description = new Text("Demo window"),
                Content = new Text("Demo content"),
                NamespaceRefs = ["demo"]
            };
        }
    }

    private sealed class QueueLlmBridge : ILLMBridge
    {
        private readonly Queue<LLMResponse> _responses;

        public QueueLlmBridge(IEnumerable<LLMResponse> responses)
        {
            _responses = new Queue<LLMResponse>(responses);
        }

        public int CallCount { get; private set; }

        public Task<LLMResponse> SendAsync(IEnumerable<LlmMessage> messages, CancellationToken ct = default)
        {
            CallCount++;
            if (_responses.Count == 0)
            {
                return Task.FromResult(LLMResponse.Fail("no queued llm response"));
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class ConstantLlmBridge : ILLMBridge
    {
        private readonly LLMResponse _response;

        public ConstantLlmBridge(LLMResponse response)
        {
            _response = response;
        }

        public Task<LLMResponse> SendAsync(IEnumerable<LlmMessage> messages, CancellationToken ct = default)
        {
            return Task.FromResult(_response);
        }
    }
}
