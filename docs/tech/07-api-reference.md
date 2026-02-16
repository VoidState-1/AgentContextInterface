# API 参考（最新版）

> 以下为当前后端实现中最常用接口（Agent 维度）。

## 1. 交互
### 1.1 发送用户消息
`POST /api/sessions/{sessionId}/agents/{agentId}/interactions`

请求示例：
```json
{
  "message": "请查看文件"
}
```

返回重点：
- `success`
- `response`
- `steps[]`：工具调用轨迹

### 1.2 注入 assistant 输出
`POST /api/sessions/{sessionId}/agents/{agentId}/interactions/assistant-output`

请求示例：
```json
{
  "assistantOutput": "<tool_call>{\"calls\":[...]}</tool_call>"
}
```

## 2. 窗口
### 2.1 获取窗口列表
`GET /api/sessions/{sessionId}/agents/{agentId}/windows`

返回窗口字段（关键）：
- `id`
- `description`
- `content`（渲染后的 XML）
- `namespaces`（窗口可见命名空间）
- `appName`

### 2.2 获取单个窗口
`GET /api/sessions/{sessionId}/agents/{agentId}/windows/{windowId}`

### 2.3 执行窗口工具
`POST /api/sessions/{sessionId}/agents/{agentId}/windows/{windowId}/actions/{actionId}`

说明：
- `actionId` 支持完整名（如 `system.close`）。
- 请求体可携带 `params`。

请求示例：
```json
{
  "params": {
    "summary": "done"
  }
}
```

## 3. 调用规范建议
- 优先使用 `namespace.tool`。
- 短名只在唯一可解析时使用。
- 异步与调用 ID 由系统管理，不在请求中手填。
