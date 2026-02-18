using ACI.Core.Models;
using ACI.Core.Services;
using ACI.Framework.Components;
using ACI.Framework.Runtime;
using ACI.LLM.Abstractions;
using ACI.Server.Services;
using ACI.Server.Settings;

namespace ACI.Server.Tests.EndToEnd;

public class SessionEndToEndTests
{
    // 测试点：普通文本交互应走无工具调用路径并直接返回 assistant 内容。
    // 预期结果：Success=true，Response 为模型文本，Steps 为空。
    [Fact]
    public async Task ProcessAsync_PlainText_ShouldReturnDirectResponse()
    {
        using var session = CreateSession(new QueueLlmBridge([LLMResponse.Ok("plain response")]));

        var result = await session.RunSerializedAsync(() => session.Interaction.ProcessAsync("hello"));

        Assert.True(result.Success);
        Assert.Equal("plain response", result.Response);
        Assert.True(result.Steps == null || result.Steps.Count == 0);
    }

    // 测试点：模型先返回 action_call 再返回文本时，系统应自动完成一轮工具执行并结束。
    // 预期结果：最终响应为第二条文本，且保留 1 条成功步骤记录。
    [Fact]
    public async Task ProcessAsync_ToolCallThenText_ShouldExecuteAndFinish()
    {
        using var session = CreateSession(new QueueLlmBridge(
        [
            LLMResponse.Ok("""
                           <action_call>
                           {"calls":[{"window_id":"e2e_window","action_id":"e2e.sync_echo","params":{"text":"hello"}}]}
                           </action_call>
                           """),
            LLMResponse.Ok("final answer")
        ]));
        session.Host.Launch("e2e_app");

        var result = await session.RunSerializedAsync(() => session.Interaction.ProcessAsync("run tool"));

        Assert.True(result.Success);
        Assert.Equal("final answer", result.Response);
        Assert.NotNull(result.Steps);
        var step = Assert.Single(result.Steps!);
        Assert.Equal("sync", step.ResolvedMode);
        Assert.True(step.Success);
    }

    // 测试点：异步动作在会话里应返回 async 步骤且携带 taskId，不阻塞主流程响应。
    // 预期结果：步骤模式为 async，taskId 非空，并能观测到后台任务生命周期事件。
    [Fact]
    public async Task ProcessAsync_AsyncToolCall_ShouldReturnAsyncStepAndTaskEvents()
    {
        using var session = CreateSession(new QueueLlmBridge(
        [
            LLMResponse.Ok("""
                           <action_call>
                           {"calls":[{"window_id":"e2e_window","action_id":"e2e.async_job"}]}
                           </action_call>
                           """),
            LLMResponse.Ok("continue talking")
        ]));
        session.Host.Launch("e2e_app");
        var events = new List<BackgroundTaskLifecycleEvent>();
        using var sub = session.Events.Subscribe<BackgroundTaskLifecycleEvent>(evt => events.Add(evt));

        var result = await session.RunSerializedAsync(() => session.Interaction.ProcessAsync("start async"));
        await Task.Delay(120);

        Assert.True(result.Success);
        Assert.Equal("continue talking", result.Response);
        Assert.NotNull(result.Steps);
        var step = Assert.Single(result.Steps!);
        Assert.Equal("async", step.ResolvedMode);
        Assert.False(string.IsNullOrWhiteSpace(step.TaskId));
        Assert.Contains(events, evt => evt.Status == BackgroundTaskStatus.Started && evt.Source == "interaction_action");
    }

    // 测试点：assistant 模拟输出支持一个 action_call 中多条 calls 顺序执行。
    // 预期结果：Steps 数量与 calls 一致，Turn 固定为 1，Index 按 1..N 递增。
    [Fact]
    public async Task ProcessAssistantOutputAsync_MultipleCalls_ShouldKeepStepOrder()
    {
        using var session = CreateSession(new QueueLlmBridge([]));
        session.Host.Launch("e2e_app");

        var output = """
                     <action_call>
                     {"calls":[
                       {"window_id":"e2e_window","action_id":"e2e.sync_echo","params":{"text":"one"}},
                       {"window_id":"e2e_window","action_id":"e2e.sync_echo","params":{"text":"two"}}
                     ]}
                     </action_call>
                     """;

        var result = await session.RunSerializedAsync(() => session.Interaction.ProcessAssistantOutputAsync(output));

        Assert.True(result.Success);
        Assert.NotNull(result.Steps);
        Assert.Equal(2, result.Steps!.Count);
        Assert.Equal(1, result.Steps[0].Turn);
        Assert.Equal(1, result.Steps[0].Index);
        Assert.Equal(1, result.Steps[1].Turn);
        Assert.Equal(2, result.Steps[1].Index);
    }

    // 测试点：非法 action_call JSON 不应导致崩溃，应按普通文本处理。
    // 预期结果：Success=true 且 Steps 为空，Response 原样返回。
    [Fact]
    public async Task ProcessAssistantOutputAsync_InvalidToolCallJson_ShouldFallbackToText()
    {
        using var session = CreateSession(new QueueLlmBridge([]));
        var output = "<action_call>{invalid json}</action_call>";

        var result = await session.RunSerializedAsync(() => session.Interaction.ProcessAssistantOutputAsync(output));

        Assert.True(result.Success);
        Assert.Equal(output, result.Response);
        Assert.True(result.Steps == null || result.Steps.Count == 0);
    }

    private static AgentContext CreateSession(ILLMBridge llm)
    {
        return new AgentContext(
            new AgentProfile { Id = "e2e-session", Name = "E2E Agent" },
            llm,
            new ACIOptions
            {
                Render = new ContextRenderOptions
                {
                    MaxTokens = 4000,
                    MinConversationTokens = 1000,
                    PruneTargetTokens = 2000
                }
            },
            configureApps: host => host.Register(new E2EApp()));
    }

    private sealed class E2EApp : ContextApp
    {
        public override string Name => "e2e_app";
        private const string WindowId = "e2e_window";
        private const string NamespaceId = "e2e";

        public override void OnCreate()
        {
            RegisterActionNamespace(NamespaceId,
            [
                new ContextAction
                {
                    Id = "sync_echo",
                    Description = "Echo text input.",
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
                    Description = "Run async job and continue interaction.",
                    Handler = async _ =>
                    {
                        await Task.Delay(30);
                        return ActionResult.Ok(message: "async done", shouldRefresh: false);
                    }
                }.AsAsync()
            ]);
        }

        public override ContextWindow CreateWindow(string? intent)
        {
            RegisterWindow(WindowId);

            return new ContextWindow
            {
                Id = WindowId,
                Description = new Text("E2E App"),
                Content = new Text("E2E content"),
                NamespaceRefs = [NamespaceId, "system"]
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

        public Task<LLMResponse> SendAsync(IEnumerable<LlmMessage> messages, CancellationToken ct = default)
        {
            if (_responses.Count == 0)
            {
                return Task.FromResult(LLMResponse.Fail("no queued response"));
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }
}
