# API 参考手册（最新版）

> 对应实现：`src/ACI.Server/Endpoints`、`src/ACI.Server/Hubs`

## 1. 基础信息

- Base URL：`http://localhost:5000`（按本地运行配置）
- 协议：HTTP + SignalR（WebSocket）
- 内容类型：`application/json`
- 认证：当前未内置鉴权

## 2. Sessions API

### 2.1 `GET /api/sessions`

返回当前活动会话列表。

### 2.2 `POST /api/sessions`

创建会话。

示例响应：

```json
{
  "sessionId": "9f4e...",
  "createdAt": "2026-02-09T00:00:00Z"
}
```

### 2.3 `GET /api/sessions/{sessionId}`

返回会话基本信息与窗口数量。

### 2.4 `GET /api/sessions/{sessionId}/context`

查询上下文时间线（结构化）。

查询参数：

- `includeObsolete`：`true` 返回归档（含过时/裁剪条目），`false` 返回活跃上下文

### 2.5 `GET /api/sessions/{sessionId}/context/raw`

返回上下文拼接后的纯文本。

查询参数：

- `includeObsolete`

### 2.6 `GET /api/sessions/{sessionId}/llm-input/raw`

返回当前将发送给 LLM 的消息快照文本（已按当前裁剪与渲染规则生成）。

### 2.7 `GET /api/sessions/{sessionId}/apps`

返回当前会话可用应用列表。

### 2.8 `DELETE /api/sessions/{sessionId}`

关闭会话并释放资源。

## 3. Interaction API

前缀：`/api/sessions/{sessionId}/interact`

### 3.1 `POST /`

请求体：

```json
{
  "message": "请帮我打开文件浏览器"
}
```

响应模型：`InteractionResponse`

关键字段：

- `success`
- `response`
- `action`（最后一个解析到的 action）
- `actionResult`（最后一个 action 结果）
- `steps`（本次所有工具步骤）
- `usage`

### 3.2 `POST /simulate`

用于手动注入 assistant 输出（调试）。

请求体：

```json
{
  "assistantOutput": "<tool_call>{\"calls\":[...]}</tool_call>"
}
```

## 4. Windows API

前缀：`/api/sessions/{sessionId}/windows`

### 4.1 `GET /`

返回窗口列表，包含：

- `id`
- `description`
- `content`（完整 `window.Render()`）
- `appName`
- `createdAt` / `updatedAt`
- `actions`（含 `paramSchema`）

### 4.2 `GET /{windowId}`

返回单个窗口详情（字段同上）。

### 4.3 `POST /{windowId}/actions/{actionId}`

直接执行窗口 action。

请求体：

```json
{
  "params": {
    "path": "C:\\"
  }
}
```

响应示例：

```json
{
  "success": true,
  "message": "Opened directory: C:\\",
  "summary": "Open directory C:\\"
}
```

## 5. Health API

### 5.1 `GET /health`

响应示例：

```json
{
  "status": "Healthy",
  "time": "2026-02-09T00:00:00Z"
}
```

## 6. SignalR Hub

- URL：`/hubs/ACI`
- 客户端调用：
  - `JoinSession(sessionId)`
  - `LeaveSession(sessionId)`
- 服务器事件：
  - `JoinedSession`
  - `LeftSession`
  - `WindowCreated`
  - `WindowUpdated`
  - `WindowClosed`
  - `Error`

## 7. 主要 DTO

`ApiModels.cs` 中定义：

- `MessageRequest`
- `SimulateRequest`
- `InteractionResponse`
- `ActionInfo`
- `ActionResultInfo`
- `InteractionStepInfo`
- `TokenUsageInfo`
- `ActionRequest`（在 `WindowEndpoints.cs`）

## 8. 错误与状态码

常见模式：

- `404`：会话或窗口不存在
- `400`：请求体为空/参数非法
- `200`：交互本身失败时也可能返回 `success=false`（请检查响应体字段）

## 9. 快速调用示例

```bash
# 创建会话
curl -X POST http://localhost:5000/api/sessions

# 发起交互
curl -X POST http://localhost:5000/api/sessions/{sessionId}/interact/ \
  -H "Content-Type: application/json" \
  -d '{"message":"打开文件浏览器"}'

# 查看窗口
curl http://localhost:5000/api/sessions/{sessionId}/windows/

# 执行窗口动作
curl -X POST http://localhost:5000/api/sessions/{sessionId}/windows/{windowId}/actions/open_path \
  -H "Content-Type: application/json" \
  -d '{"params":{"path":"C:\\\\"}}'
```
