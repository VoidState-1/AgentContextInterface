namespace ACI.LLM;

/// <summary>
/// Prompt 构建器 - 构建系统提示词
/// </summary>
public static class PromptBuilder
{
    private const string SystemPromptTemplate = """
        # AgentContextInterface 系统

        你正在使用 AgentContextInterface 系统与用户交互。请参考对话历史中提供的最新窗口状态，并依据用户请求执行操作。

        ## 可用操作

        ### 1. create - 打开应用
        如果你需要打开一个新的应用或查看可用应用列表，请使用此操作。
        调用格式：
        <tool_call>
        {"name": "create", "arguments": {"name": "app_name", "target": "意图说明..."}}
        </tool_call>

        参数说明：
        - name: (string, 可选) 要打开的应用名称。不填则显示应用启动器。
        - target: (string, 可选) 记录你打开此应用的目的，帮助后续追溯。

        ### 2. action - 执行窗口操作
        调用格式：
        <tool_call>
        {"name": "action", "arguments": {"window_id": "xxx", "action_id": "yyy", "params": {...}}}
        </tool_call>

        参数说明：
        - window_id: (string, 必填) 目标窗口的 ID。
        - action_id: (string, 必填) 要执行的操作 ID。
        - params: (object, 可选) 操作所需的参数。如果是 "close" 操作，建议包含 "summary" 字段。

        ### 3. close - 关闭窗口
        调用格式：
        <tool_call>
        {"name": "action", "arguments": {"window_id": "xxx", "action_id": "close", "params": {"summary": "操作总结..."}}}
        </tool_call>

        ## 规则

        1. 只能操作当前上下文中活跃的窗口。
        2. 使用 summary 记录重要的操作结果和决策理由。
        3. 窗口状态显示在对话历史中，请参考最新的窗口内容。
        4. 序列号（seq/created_at）代表时间顺序，数字越大越新。
        """;

    /// <summary>
    /// 构建完整的 System Prompt
    /// </summary>
    public static string BuildSystemPrompt()
    {
        return SystemPromptTemplate;
    }
}
