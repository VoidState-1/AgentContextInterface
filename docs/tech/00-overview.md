# ACI 系统总览（最新版）

## 1. 定位
AgentContextInterface（ACI）是一个窗口化、多工具、可循环调用的 Agent 交互系统。

当前核心原则：
- 窗口是上下文中的普通条目，按时间线穿插渲染。
- 工具不再由窗口内 `actions` 直接描述，而是由命名空间（namespace）统一描述。
- 窗口通过 `ns`（命名空间引用）声明可见工具集合。
- 模型输出 `<tool_call>` 后系统自动继续循环，直到出现非工具文本或达到上限。

## 2. tool_call 协议
```xml
<tool_call>
{"calls":[{"window_id":"xxx","action_id":"namespace.tool","params":{"k":"v"}}]}
</tool_call>
```

字段规则：
- `calls`：必填数组。
- `window_id`：必填。
- `action_id`：必填，推荐完整写法 `namespace.tool`。
- `params`：可选对象。

补充规则：
- 短名（如 `close`）仅在当前窗口可见命名空间内唯一时允许。
- 歧义短名会被系统拒绝并返回错误。
- `call_id` 与执行模式由系统决定，模型不填写。

## 3. 上下文渲染规则
- `ContextItem` 顺序保持不变。
- `Window` 条目仍以 `<Window ...>` 形式穿插出现。
- 渲染器会按当前活跃窗口的 `ns` 引用，按需注入 `<namespace id="...">...</namespace>`。
- 同一命名空间在一次渲染中只注入一次；没有引用时自动消失。

## 4. 模块职责
- `ACI.Core`：窗口模型、上下文存储与裁剪、动作执行、命名空间注册表。
- `ACI.Framework`：应用运行时、窗口定义、内置应用、工具命名空间注册。
- `ACI.LLM`：提示词、tool_call 解析、命名空间工具解析、交互循环。
- `ACI.Server`：会话管理、API/SignalR、Agent 运行容器。

## 5. 关键对象
- `Window`：包含 `id/content/description/ns/options/meta`。
- `ToolNamespaceDefinition`：命名空间及其工具定义。
- `ToolDescriptor`：`id/params/description`（运行时还可含执行模式元数据）。
- `ToolNamespaceRegistry`：命名空间注册与查询。
