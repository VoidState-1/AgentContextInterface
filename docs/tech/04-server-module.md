# Server 模块（最新版）

`ACI.Server` 负责会话容器、HTTP API、实时通知与持久化编排。

## 1. 核心职责

- 管理 Session 生命周期（创建、关闭、恢复）
- 管理 Session 下多个 Agent 的交互入口
- 暴露 REST API（Session / Agent / Interaction / Window / Persistence）
- 通过 SignalR 广播窗口变更事件
- 在会话状态变更后触发自动保存

## 2. 关键对象

### 2.1 `SessionManager`
- 维护活跃 Session 字典
- 创建/销毁 Session
- 负责保存、加载、删除快照
- 绑定窗口事件并转发到 Hub

### 2.2 `Session`
- 多 Agent 容器
- 对外提供：`InteractAsync`、`SimulateAsync`、`ExecuteWindowActionAsync`
- 内部维护跨 Agent 唤醒队列与消息桥接

### 2.3 `AgentContext`
- 聚合 Core / Framework / LLM 三层能力
- 承载单 Agent 串行执行锁
- 负责窗口事件与上下文时间线同步

## 3. API 分组

- `SessionEndpoints`：Session 与 Agent 查询接口
- `InteractionEndpoints`：交互与模拟接口
- `WindowEndpoints`：窗口查询与 Action 执行
- `PersistenceEndpoints`：保存/加载/删除快照

当前端点响应已统一使用明确 DTO（位于 `src/ACI.Server/Dto`），不再依赖匿名对象返回。

## 4. 实时通知

Hub：`/hubs/ACI`

事件：
- `WindowCreated`
- `WindowUpdated`
- `WindowClosed`

## 5. 配置入口

`appsettings.json` -> `ACI` 节点：
- `Render`（上下文渲染与裁剪参数）
- `Persistence`（存储路径与自动保存）

OpenRouter 配置通过 `OpenRouter` 节点注入。
