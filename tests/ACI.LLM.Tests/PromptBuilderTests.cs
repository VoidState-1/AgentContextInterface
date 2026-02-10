namespace ACI.LLM.Tests;

public class PromptBuilderTests
{
    // 测试点：系统提示词应包含最新 tool_call 协议关键字段说明。
    // 预期结果：文本中包含 calls/window_id/action_id/params 约定。
    [Fact]
    public void BuildSystemPrompt_ShouldContainToolCallProtocol()
    {
        var prompt = PromptBuilder.BuildSystemPrompt();

        Assert.Contains("\"calls\"", prompt);
        Assert.Contains("\"window_id\"", prompt);
        Assert.Contains("\"action_id\"", prompt);
        Assert.Contains("\"params\"", prompt);
    }
}
