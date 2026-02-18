using ACI.LLM.Services;

namespace ACI.LLM.Tests.Services;

public class ActionParserTests
{
    // 测试点：非 action_call 文本不应被解析为动作。
    // 预期结果：返回 null。
    [Fact]
    public void Parse_WithoutToolCallTag_ShouldReturnNull()
    {
        var result = ActionParser.Parse("plain assistant response");

        Assert.Null(result);
    }

    // 测试点：兼容单条调用格式（window_id/action_id/params）。
    // 预期结果：返回包含 1 条调用的批次结果。
    [Fact]
    public void Parse_SingleCallPayload_ShouldReturnOneCall()
    {
        var content = """
                      <action_call>
                      {"window_id":"w1","action_id":"open","params":{"path":"C:\\"}}
                      </action_call>
                      """;

        var result = ActionParser.Parse(content);

        Assert.NotNull(result);
        var call = Assert.Single(result!.Calls);
        Assert.Equal("w1", call.WindowId);
        Assert.Equal("open", call.ActionId);
        Assert.NotNull(call.Parameters);
        Assert.Equal("C:\\", call.Parameters!.Value.GetProperty("path").GetString());
    }

    // 测试点：批量调用格式应按模型输出顺序保留。
    // 预期结果：返回的 Calls 顺序与 JSON 数组顺序一致。
    [Fact]
    public void Parse_BatchCallPayload_ShouldKeepOrder()
    {
        var content = """
                      <action_call>
                      {"calls":[{"window_id":"w1","action_id":"a1"},{"window_id":"w2","action_id":"a2","params":{"v":1}}]}
                      </action_call>
                      """;

        var result = ActionParser.Parse(content);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Calls.Count);
        Assert.Equal("w1", result.Calls[0].WindowId);
        Assert.Equal("a1", result.Calls[0].ActionId);
        Assert.Equal("w2", result.Calls[1].WindowId);
        Assert.Equal("a2", result.Calls[1].ActionId);
    }

    // 测试点：调用条目缺少关键字段时应拒绝解析。
    // 预期结果：返回 null，避免进入执行流程。
    [Fact]
    public void Parse_BatchCallMissingRequiredField_ShouldReturnNull()
    {
        var content = """
                      <action_call>
                      {"calls":[{"window_id":"w1"},{"window_id":"w2","action_id":"a2"}]}
                      </action_call>
                      """;

        var result = ActionParser.Parse(content);

        Assert.Null(result);
    }

    // 测试点：params 不是对象时应视为无参数，而非解析失败。
    // 预期结果：调用可解析成功，Parameters 为 null。
    [Fact]
    public void Parse_ParamsNotObject_ShouldSetParametersNull()
    {
        var content = """
                      <action_call>
                      {"window_id":"w1","action_id":"run","params":"not-object"}
                      </action_call>
                      """;

        var result = ActionParser.Parse(content);

        Assert.NotNull(result);
        var call = Assert.Single(result!.Calls);
        Assert.Null(call.Parameters);
    }

    // 测试点：action_call 内部 JSON 非法时应安全失败。
    // 预期结果：返回 null，不抛出异常。
    [Fact]
    public void Parse_InvalidJson_ShouldReturnNull()
    {
        var content = "<action_call>{invalid json}</action_call>";

        var result = ActionParser.Parse(content);

        Assert.Null(result);
    }
}
