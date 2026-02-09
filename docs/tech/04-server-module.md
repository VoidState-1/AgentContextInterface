# Server 模块详解（最新版）

> 对应代码：`src/ACI.Server`

## 1. 模块职责

Server 是对外入口层，负责：

1. Minimal API 端点
2. SignalR 实时通知
3. 会话容器创建与回收
4. 把请求串行化到单个会话上下文执行

## 2. 关键组件

### 2.1 SessionManager

接口：

- `CreateSession(string? sessionId = null)`
- `GetSession(string sessionId)`
- `CloseSession(string sessionId)`
- `GetActiveSessions()`

实现特征：

- 会话字典：`ConcurrentDictionary<string, SessionContext>`
- 自动绑定窗口事件到 Hub 通知

### 2.2 SessionContext

每个会话持有完整依赖：

- Core：`Clock/Events/Windows/Context`
- Framework：`Runtime/Host`
- LLM：`Interaction`
- 执行器：`ActionExecutor`
- 后台任务：`SessionTaskRunner`

关键初始化行为：

1. 读取 `ACIOptions.Render`
2. 构建 Core + Framework + LLM 组件
3. 注册内置应用：`launcher`、`activity_log`、`file_explorer`
4. 启动 `activity_log`
5. 启动常驻 `launcher`

### 2.3 会话串行执行模型

`SessionContext` 使用 `SemaphoreSlim` 保证同一会话内操作串行化：

- HTTP 端点调用 `RunSerializedAsync(...)`
- 后台任务回写也通过 `RunSerializedActionAsync(...)` 回到同一串行上下文

## 3. 后台任务接线

`SessionTaskRunner` 负责：

- `Start(windowId, taskBody, taskId, source)`
- `Cancel(taskId)`
- 生命周期事件发布（`BackgroundTaskLifecycleEvent`）

状态流：

- `Started`
- `Completed`
- `Failed`
- `Canceled`

## 4. HTTP 端点分组

### 4.1 Sessions

前缀：`/api/sessions`

- `GET /`：会话列表
- `POST /`：创建会话
- `GET /{sessionId}`：会话详情
- `GET /{sessionId}/context?includeObsolete={bool}`：上下文时间线（结构化）
- `GET /{sessionId}/context/raw?includeObsolete={bool}`：上下文原始文本
- `GET /{sessionId}/llm-input/raw`：当前发给 LLM 的消息快照文本
- `GET /{sessionId}/apps`：可用应用列表
- `DELETE /{sessionId}`：关闭会话

### 4.2 Interaction

前缀：`/api/sessions/{sessionId}/interact`

- `POST /`：标准交互（用户消息）
- `POST /simulate`：手动注入 assistant 输出（调试）

### 4.3 Windows

前缀：`/api/sessions/{sessionId}/windows`

- `GET /`：窗口列表
- `GET /{windowId}`：窗口详情
- `POST /{windowId}/actions/{actionId}`：直接执行窗口 action

## 5. DTO（当前）

关键类型：

- `MessageRequest`
- `SimulateRequest`
- `InteractionResponse`
- `InteractionStepInfo`
- `ActionRequest`

`InteractionResponse` 新增 `Steps`，用于回传每个 tool_call 的执行轨迹。

## 6. SignalR

Hub 路径：`/hubs/ACI`

客户端方法：

- `JoinSession(sessionId)`
- `LeaveSession(sessionId)`

推送事件：

- `WindowCreated`
- `WindowUpdated`
- `WindowClosed`
- `JoinedSession`
- `LeftSession`
- `Error`

## 7. Program.cs 注册（当前）

- CORS（`AllowAnyOrigin/Method/Header`）
- SignalR
- `OpenRouterConfig` + `ACIOptions`
- `HttpClient<ILLMBridge, OpenRouterClient>`
- `ISessionManager`（Singleton）
- `IACIHubNotifier`（Singleton）
- Swagger（开发环境）

## 8. 配置项（appsettings.json）

`ACI.Render`：

- `MaxTokens`
- `MinConversationTokens`
- `PruneTargetTokens`

`ACI.Context`：

- `MaxItems`（当前保留字段，尚未接入裁剪逻辑）

`OpenRouter`：

- `BaseUrl`
- `ApiKey`
- `DefaultModel`
- `FallbackModels`
- `MaxTokens`
- `Temperature`
- `TimeoutSeconds`
- `MaxRetries`

## 9. 目录结构

```text
ACI.Server/
  Endpoints/
    InteractionEndpoints.cs
    SessionEndpoints.cs
    WindowEndpoints.cs
  Hubs/
    ACIHub.cs
  Services/
    SessionContext.cs
    SessionManager.cs
    SessionTaskRunner.cs
  Settings/
    ACIOptions.cs
  Dto/
    ApiModels.cs
  Program.cs
  appsettings.json
```
