# API 参考（最新版）

本文档描述当前后端已落地接口（以 Session + Agent 维度为主）。

## 1. Session

### 1.1 创建 Session
- `POST /api/sessions/`
- 请求体：可选 `CreateSessionRequest`
- 响应：`SessionSummaryResponse`

### 1.2 查询 Session 列表
- `GET /api/sessions/`
- 响应：`SessionSummaryResponse[]`

### 1.3 查询单个 Session
- `GET /api/sessions/{sessionId}`
- 响应：`SessionSummaryResponse`

### 1.4 关闭 Session
- `DELETE /api/sessions/{sessionId}`
- 响应：`204 NoContent`

## 2. Agent

### 2.1 查询 Agent 列表
- `GET /api/sessions/{sessionId}/agents/`
- 响应：`AgentSummaryResponse[]`

### 2.2 查询上下文时间线
- `GET /api/sessions/{sessionId}/agents/{agentId}/context?includeObsolete=true|false`
- 响应：`ContextTimelineItemResponse[]`

### 2.3 查询原始 LLM 输入
- `GET /api/sessions/{sessionId}/agents/{agentId}/llm-input/raw`
- 响应：`text/plain`

### 2.4 查询可用应用
- `GET /api/sessions/{sessionId}/agents/{agentId}/apps`
- 响应：`AppSummaryResponse[]`

## 3. Interaction

### 3.1 用户消息交互
- `POST /api/sessions/{sessionId}/agents/{agentId}/interact/`
- 请求体：
```json
{
  "message": "请打开文件浏览器"
}
```
- 响应：`InteractionResponse`

### 3.2 模拟 Assistant 输出
- `POST /api/sessions/{sessionId}/agents/{agentId}/interact/simulate`
- 请求体：
```json
{
  "assistantOutput": "<action_call>{\"calls\":[...]}</action_call>"
}
```
- 响应：`InteractionResponse`

## 4. Window

### 4.1 查询窗口列表
- `GET /api/sessions/{sessionId}/agents/{agentId}/windows/`
- 响应：`WindowSummaryResponse[]`

关键字段：
- `id`
- `description`
- `content`
- `namespaces`
- `appName`
- `createdAt` / `updatedAt`

### 4.2 查询单个窗口
- `GET /api/sessions/{sessionId}/agents/{agentId}/windows/{windowId}`
- 响应：`WindowSummaryResponse`

### 4.3 执行窗口 Action
- `POST /api/sessions/{sessionId}/agents/{agentId}/windows/{windowId}/actions/{actionId}`
- 请求体：
```json
{
  "params": {
    "summary": "done"
  }
}
```
- 响应：`WindowActionInvokeResponse`

说明：
- 推荐使用完整 Action ID：`namespace.action`（例如 `system.close`）
- 短名仅在可唯一解析时使用

## 5. Persistence

### 5.1 保存 Session
- `POST /api/sessions/{sessionId}/save`
- 响应：`SaveSessionResponse`

### 5.2 加载 Session
- `POST /api/sessions/{sessionId}/load`
- 响应：`SessionSummaryResponse`

### 5.3 查询已保存 Session
- `GET /api/sessions/saved`
- 响应：`SessionSummary[]`

### 5.4 删除已保存 Session
- `DELETE /api/sessions/saved/{sessionId}`
- 响应：`204 NoContent`

## 6. Action Call 协议

```xml
<action_call>
{"calls":[{"window_id":"xxx","action_id":"namespace.action","params":{...}}]}
</action_call>
```

字段规则：
- `calls`：必填数组
- `window_id`：必填
- `action_id`：必填，推荐 `namespace.action`
- `params`：可选对象

补充规则：
- `call_id` 由系统分配，不需要模型填写
- 执行模式由后端 Action 元数据决定，不需要模型填写
