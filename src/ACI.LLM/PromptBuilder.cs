using System.Text;
using ACI.Framework.Runtime;

namespace ACI.LLM;

/// <summary>
/// 构建系统提示词。
/// </summary>
public static class PromptBuilder
{
    // action_call 协议：使用 calls 数组，系统自动处理调用 ID 和执行模式。
    private const string SystemPromptTemplate = """
        # AgentContextInterface System

        You are operating inside AgentContextInterface.
        Always read the latest window state from the conversation context before deciding what to do.

        ## Action Call Format

        Use exactly this payload inside `<action_call>...</action_call>`:

        <action_call>
        {"calls":[{"window_id":"xxx","action_id":"yyy","params":{...}}]}
        </action_call>

        Field rules:
        - calls: required array
        - window_id: required, target window id
        - action_id: required, action id. Prefer `namespace.action` (for example `system.close`).
        - params: optional object, action parameters

        Notes:
        - Do not provide call id; the system assigns it.
        - Do not provide execution mode; mode is defined by the action metadata.
        - Short action id is allowed only when it is unambiguous in the window's visible namespaces.

        ## Launcher Rules

        - `launcher` window is always present.
        - Use launcher actions (for example `launcher.open`) to open applications.
        - `launcher` cannot be closed.

        ## General Rules

        1. Only operate on active windows that exist in context.
        2. Use `summary` when closing a window if an action supports it.
        3. Prefer the most recent window state (`seq` / `created_at` / `updated_at`).
        """;

    /// <summary>
    /// 返回完整系统提示词。
    /// 传入 AgentProfile 时会追加 Agent 身份和角色描述。
    /// </summary>
    public static string BuildSystemPrompt(AgentProfile? profile = null)
    {
        if (profile == null || profile.Id == "default")
        {
            return SystemPromptTemplate;
        }

        var sb = new StringBuilder(SystemPromptTemplate);

        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("## Your Identity");
        sb.AppendLine($"- Agent ID: {profile.Id}");
        sb.AppendLine($"- Name: {profile.Name}");

        if (!string.IsNullOrWhiteSpace(profile.Role))
        {
            sb.AppendLine();
            sb.AppendLine("## Your Role");
            sb.AppendLine(profile.Role);
        }

        return sb.ToString();
    }
}
