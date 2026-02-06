# API 参考手册

> 本文档提供 ACI Server 的完整 API 参考。

## 1. 概述

### 1.1 基础信息

| 项目 | 值 |
|------|-----|
| Base URL | `http://localhost:5000` |
| 协议 | HTTP/1.1, WebSocket (SignalR) |
| 内容类型 | `application/json` |
| 认证 | 无（开发模式） |

### 1.2 通用响应格式

**成功响应**
```json
{
  "success": true,
  "data": { ... }
}
```

**错误响应**
```json
{
  "error": "错误描述"
}
```

---

## 2. 会话管理 API

### 2.1 获取所有会话

```http
GET /api/sessions
```

**响应**
```json
[
  {
    "sessionId": "abc123def456",
    "createdAt": "2024-01-20T10:30:00Z"
  }
]
```

---

### 2.2 创建会话

```http
POST /api/sessions
```

**响应**
```json
{
  "sessionId": "abc123def456",
  "createdAt": "2024-01-20T10:30:00Z"
}
```

**状态码**
| 代码 | 说明 |
|------|------|
| 201 | 创建成功 |

---

### 2.3 获取会话详情

```http
GET /api/sessions/{sessionId}
```

**参数**
| 名称 | 位置 | 必需 | 说明 |
|------|------|------|------|
| sessionId | path | 是 | 会话 ID |

**响应**
```json
{
  "sessionId": "abc123def456",
  "createdAt": "2024-01-20T10:30:00Z",
  "windowCount": 2
}
```

**状态码**
| 代码 | 说明 |
|------|------|
| 200 | 成功 |
| 404 | 会话不存在 |

---

### 2.4 关闭会话

```http
DELETE /api/sessions/{sessionId}
```

**参数**
| 名称 | 位置 | 必需 | 说明 |
|------|------|------|------|
| sessionId | path | 是 | 会话 ID |

**状态码**
| 代码 | 说明 |
|------|------|
| 204 | 成功关闭 |
| 404 | 会话不存在 |

---

## 3. 交互 API

### 3.1 发送消息

与 AI 进行交互。

```http
POST /api/sessions/{sessionId}/interact
Content-Type: application/json
```

**参数**
| 名称 | 位置 | 必需 | 说明 |
|------|------|------|------|
| sessionId | path | 是 | 会话 ID |

**请求体**
```json
{
  "message": "帮我创建一个待办列表"
}
```

**响应**
```json
{
  "success": true,
  "response": "好的，我来帮你创建待办列表。\n<tool_call>{\"name\": \"create\", \"arguments\": {\"name\": \"todo\"}}</tool_call>",
  "action": {
    "type": "create",
    "appName": "todo",
    "windowId": null,
    "actionId": null
  },
  "actionResult": {
    "success": true,
    "message": "已打开应用: todo",
    "summary": null
  },
  "usage": {
    "promptTokens": 1500,
    "completionTokens": 200,
    "totalTokens": 1700
  }
}
```

**错误响应**
```json
{
  "success": false,
  "error": "LLM 调用失败: API 超时"
}
```

**状态码**
| 代码 | 说明 |
|------|------|
| 200 | 成功（检查 success 字段）|
| 400 | 消息为空 |
| 404 | 会话不存在 |

---

## 4. 窗口 API

### 4.1 获取所有窗口

```http
GET /api/sessions/{sessionId}/windows
```

**响应**
```json
[
  {
    "id": "todo_12345",
    "description": "<p>待办事项管理应用</p>",
    "content": "<item id=\"1\">买菜</item><item id=\"2\">写代码</item>",
    "appName": "todo",
    "createdAt": 10,
    "updatedAt": 15
  }
]
```

---

### 4.2 获取窗口详情

```http
GET /api/sessions/{sessionId}/windows/{windowId}
```

**响应**
```json
{
  "id": "todo_12345",
  "description": "<p>待办事项管理应用</p>",
  "content": "<item id=\"1\">买菜</item><item id=\"2\">写代码</item>",
  "appName": "todo",
  "createdAt": 10,
  "updatedAt": 15,
  "actions": [
    {
      "id": "add",
      "label": "添加条目"
    },
    {
      "id": "delete",
      "label": "删除条目"
    },
    {
      "id": "close",
      "label": "关闭"
    }
  ]
}
```

**状态码**
| 代码 | 说明 |
|------|------|
| 200 | 成功 |
| 404 | 会话或窗口不存在 |

---

### 4.3 执行窗口操作

```http
POST /api/sessions/{sessionId}/windows/{windowId}/actions/{actionId}
Content-Type: application/json
```

**参数**
| 名称 | 位置 | 必需 | 说明 |
|------|------|------|------|
| sessionId | path | 是 | 会话 ID |
| windowId | path | 是 | 窗口 ID |
| actionId | path | 是 | 操作 ID |

**请求体**
```json
{
  "params": {
    "text": "看电影"
  }
}
```

**响应**
```json
{
  "success": true,
  "message": "已添加条目",
  "summary": null
}
```

**特殊操作：关闭窗口**
```http
POST /api/sessions/{sessionId}/windows/{windowId}/actions/close
Content-Type: application/json

{
  "params": {
    "summary": "完成了3个待办事项"
  }
}
```

**状态码**
| 代码 | 说明 |
|------|------|
| 200 | 成功 |
| 400 | 窗口不支持操作 |
| 404 | 会话或窗口不存在 |

---

## 5. 系统 API

### 5.1 健康检查

```http
GET /health
```

**响应**
```json
{
  "status": "Healthy",
  "time": "2024-01-20T10:30:00Z"
}
```

---

## 6. SignalR Hub

### 6.1 连接

```
WebSocket: /hubs/ACI
```

### 6.2 客户端方法

#### JoinSession

加入会话，开始接收该会话的实时更新。

```javascript
connection.invoke("JoinSession", sessionId);
```

**参数**
| 名称 | 类型 | 说明 |
|------|------|------|
| sessionId | string | 会话 ID |

---

#### LeaveSession

离开会话，停止接收更新。

```javascript
connection.invoke("LeaveSession", sessionId);
```

**参数**
| 名称 | 类型 | 说明 |
|------|------|------|
| sessionId | string | 会话 ID |

### 6.3 服务器事件

#### JoinedSession

成功加入会话时触发。

```javascript
connection.on("JoinedSession", (sessionId) => {
  console.log("已加入会话:", sessionId);
});
```

---

#### LeftSession

成功离开会话时触发。

```javascript
connection.on("LeftSession", (sessionId) => {
  console.log("已离开会话:", sessionId);
});
```

---

#### WindowCreated

新窗口创建时触发。

```javascript
connection.on("WindowCreated", (window) => {
  console.log("新窗口:", window);
});
```

**数据格式**
```json
{
  "id": "todo_12345",
  "description": "...",
  "content": "...",
  "appName": "todo",
  "createdAt": 10,
  "updatedAt": 10
}
```

---

#### WindowUpdated

窗口内容更新时触发。

```javascript
connection.on("WindowUpdated", (window) => {
  console.log("窗口更新:", window);
});
```

**数据格式**
```json
{
  "id": "todo_12345",
  "description": "...",
  "content": "...",
  "updatedAt": 15
}
```

---

#### WindowClosed

窗口关闭时触发。

```javascript
connection.on("WindowClosed", (data) => {
  console.log("窗口关闭:", data.windowId);
});
```

**数据格式**
```json
{
  "windowId": "todo_12345"
}
```

---

#### Error

发生错误时触发。

```javascript
connection.on("Error", (message) => {
  console.error("错误:", message);
});
```

---

## 7. 数据模型

### 7.1 MessageRequest

```typescript
interface MessageRequest {
  message: string;  // 用户消息，必需
}
```

### 7.2 InteractionResponse

```typescript
interface InteractionResponse {
  success: boolean;
  error?: string;
  response?: string;      // AI 响应文本
  action?: ActionInfo;    // 解析的操作
  actionResult?: ActionResultInfo;  // 操作执行结果
  usage?: TokenUsageInfo; // Token 使用统计
}
```

### 7.3 ActionInfo

```typescript
interface ActionInfo {
  type: "create" | "action";
  appName?: string;   // create 时有效
  windowId?: string;  // action 时有效
  actionId?: string;  // action 时有效
}
```

### 7.4 ActionResultInfo

```typescript
interface ActionResultInfo {
  success: boolean;
  message?: string;  // 结果消息
  summary?: string;  // 摘要（关闭时）
}
```

### 7.5 TokenUsageInfo

```typescript
interface TokenUsageInfo {
  promptTokens: number;
  completionTokens: number;
  totalTokens: number;
}
```

### 7.6 WindowInfo

```typescript
interface WindowInfo {
  id: string;
  description: string;  // 渲染后的 XML/HTML
  content: string;      // 渲染后的 XML/HTML
  appName: string;
  createdAt: number;    // Seq
  updatedAt: number;    // Seq
  actions?: ActionDefinitionInfo[];
}
```

### 7.7 ActionDefinitionInfo

```typescript
interface ActionDefinitionInfo {
  id: string;
  label: string;
}
```

---

## 8. 错误代码

| HTTP 状态码 | 说明 | 处理建议 |
|-------------|------|----------|
| 400 | 请求参数错误 | 检查请求体格式 |
| 404 | 资源不存在 | 检查会话/窗口 ID |
| 500 | 服务器内部错误 | 查看服务器日志 |
| 503 | 服务不可用 | LLM API 可能超时，重试 |

---

## 9. 使用示例

### 9.1 完整交互流程

```javascript
// 1. 创建会话
const session = await fetch('/api/sessions', { method: 'POST' }).then(r => r.json());
const sessionId = session.sessionId;

// 2. 连接 SignalR（可选，用于实时更新）
const connection = new signalR.HubConnectionBuilder()
  .withUrl('/hubs/ACI')
  .build();

connection.on("WindowCreated", w => console.log("新窗口:", w));
connection.on("WindowUpdated", w => console.log("窗口更新:", w));

await connection.start();
await connection.invoke("JoinSession", sessionId);

// 3. 发送消息
const response = await fetch(`/api/sessions/${sessionId}/interact`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ message: "帮我创建一个待办列表" })
}).then(r => r.json());

console.log("AI 响应:", response);

// 4. 获取窗口
const windows = await fetch(`/api/sessions/${sessionId}/windows`).then(r => r.json());
console.log("当前窗口:", windows);

// 5. 执行操作
await fetch(`/api/sessions/${sessionId}/windows/${windows[0].id}/actions/add`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ params: { text: "买菜" } })
});

// 6. 关闭会话
await fetch(`/api/sessions/${sessionId}`, { method: 'DELETE' });
```

### 9.2 Curl 示例

```bash
# 创建会话
curl -X POST http://localhost:5000/api/sessions

# 发送消息
curl -X POST http://localhost:5000/api/sessions/{sessionId}/interact \
  -H "Content-Type: application/json" \
  -d '{"message": "你好"}'

# 获取窗口
curl http://localhost:5000/api/sessions/{sessionId}/windows

# 执行操作
curl -X POST http://localhost:5000/api/sessions/{sessionId}/windows/{windowId}/actions/add \
  -H "Content-Type: application/json" \
  -d '{"params": {"text": "新条目"}}'
```
