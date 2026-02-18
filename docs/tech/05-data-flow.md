# 数据流与生命周期（最新版）

## 1. 用户消息主链路

1. 客户端调用 `POST /api/sessions/{sessionId}/agents/{agentId}/interact/`
2. Server 进入 `Session.InteractAsync`，在 Agent 串行上下文执行
3. `InteractionController` 渲染上下文并请求 LLM
4. 若响应包含 `<action_call>`，进入自动循环执行
5. 若响应为普通文本，返回 `InteractionResponse`

## 2. 自动 Action 循环

`InteractionOrchestrator` 在单次交互内执行：

1. 裁剪上下文（Prune）
2. 请求 LLM
3. 解析 `action_call`
4. 顺序执行 `calls[]`
5. 累积 `steps[]` 与 `usage`
6. 直到出现非 `action_call` 响应或达到轮次上限

## 3. 窗口生命周期

### 3.1 创建
1. `FrameworkHost.Launch(app, intent)`
2. 生成 `Window` 并写入 `WindowManager`
3. 触发 `WindowChangedEvent(Created)`
4. `AgentContext` 向上下文写入 `ContextItem(Window)`

### 3.2 刷新
1. `FrameworkHost.RefreshWindow(windowId)`
2. 原地替换窗口内容与命名空间引用
3. `WindowManager.NotifyUpdated`
4. 如 `RefreshMode=Append`，追加新的 `ContextItem(Window)`

### 3.3 关闭
1. 执行 `system.close` 或 action 返回 `ShouldClose=true`
2. `WindowManager.Remove(windowId)`
3. 上下文中对应窗口条目标记为 obsolete

## 4. 后台任务链路

当 action 为异步模式：

1. 交互流程返回 `taskId`
2. `SessionTaskRunner` 启动后台任务
3. 任务状态通过统一串行入口回写会话状态
4. 相关事件进入日志系统与上下文

## 5. 一致性保证

- 同一 Session 内的交互、模拟调用、直接 action 调用都走串行执行
- `ContextItem(Window)` 存储窗口引用 ID，渲染时读取窗口最新状态
- 命名空间描述按当前活跃窗口引用动态注入上下文
