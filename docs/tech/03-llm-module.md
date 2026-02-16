# LLM 模块（最新版）

## 1. 职责
`ACI.LLM` 负责：
- 组装系统提示词。
- 解析 `<tool_call>`。
- 解析工具名（`namespace.tool` 与短名消歧）。
- 驱动自动工具循环。

## 2. 关键流程
1. 读取当前上下文（含窗口与按需命名空间定义）。
2. 调用 LLM。
3. 若返回普通文本：结束本轮交互。
4. 若返回 `<tool_call>`：解析并顺序执行 `calls[]`。
5. 执行后刷新上下文，再次请求 LLM。
6. 达到最大连续工具轮次（默认 12）则安全失败。

## 3. tool_call 解析
当前使用：
- `ActionParser`：从 assistant 文本中提取 `<tool_call>` JSON。
- `ToolActionResolver`：根据窗口可见命名空间解析工具。

解析规则：
- 完整名 `namespace.tool`：直接按命名空间校验。
- 短名 `tool`：仅当唯一匹配时允许。
- 多匹配：返回歧义错误。
- 不可见命名空间：返回不可见错误。

## 4. 执行模式
- 模型不填执行模式。
- 模式来自工具元数据（`sync` / `async`）。
- `InteractionStep.ResolvedMode` 回传最终模式。

## 5. 交互返回
`InteractionResult` 主要字段：
- `Success / Error`
- `Response`
- `Action / ActionResult`
- `Steps[]`（每个工具调用的执行轨迹）
