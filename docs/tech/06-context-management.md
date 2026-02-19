# 上下文管理详解（最新版）

> 对应实现：`ContextStore` + `ContextPruner` + `ContextRenderer`

## 1. 设计目标

当前上下文系统关注三件事：
1. 保证 LLM 看到的窗口内容始终是最新状态。
2. 裁剪时真正减少活跃上下文长度，避免无效堆积。
3. 保留完整归档，便于调试和后续检索能力扩展。

## 2. 数据结构

### 2.1 ContextItem

核心字段：
- `Id`
- `Type`（`System/User/Assistant/Window`）
- `Seq`
- `Content`
- `IsObsolete`
- `EstimatedTokens`

其中 `Window` 类型条目仅存 `windowId`，渲染时再从 `WindowManager` 读取最新窗口内容。

### 2.2 双视图存储

`ContextStore` 内部维护：
- `activeItems`：参与渲染与裁剪的活跃条目
- `archiveItems`：完整历史备份
- `archiveById`：按 `Id` 的索引

`Prune` 只会物理删除 `activeItems`，不会删除 `archiveItems`。

## 3. 写入与过时标记

### 3.1 Add

新增条目时：
1. 分配 `Seq = clock.Next()`
2. 估算 token
3. 若为 `Window` 条目，先将同窗口旧活跃条目标记为 `IsObsolete=true`
4. 同时写入活跃与归档集合

### 3.2 MarkWindowObsolete

窗口关闭时，对应窗口上下文条目会被标记为过时。

## 4. 渲染规则

`ContextRenderer.Render(items, windowManager, options)`：
- `System` -> `role=system`
- `User` -> `role=user`
- `Assistant` -> `role=assistant`
- `Window` -> 读取 `windowManager.Get(windowId)` 后使用 `window.Render()`，以 `role=user` 注入

若窗口已不存在（例如已关闭），该条目会被跳过。

## 5. 裁剪规则

输入：
- `maxTokens`
- `minConversationTokens`
- `pruneTargetTokens`（若为 0，回退到 `maxTokens/2`）

### 5.1 总体流程

1. 计算活跃条目估算 token 总量
2. 若未超过 `maxTokens`，不裁剪
3. 超限后裁剪到 `pruneTargetTokens`

### 5.2 两阶段策略

阶段一：
- 优先裁剪旧对话（`User/Assistant`）
- 但对话 token 不低于 `minConversationTokens`
- 同时优先裁剪非重要窗口（`Important=false` 且未 `PinInPrompt`）

阶段二：
- 若仍超限，继续裁剪旧的重要窗口
- `PinInPrompt=true` 的窗口始终保留

## 6. 重要窗口与固定窗口

`WindowOptions` 关键字段：
- `Important`（默认 `true`）：影响裁剪优先级
- `PinInPrompt`（默认 `false`）：强制保留在提示词中

典型用法：
- `launcher`：`PinInPrompt=true`，确保模型随时可以打开应用
- 临时日志窗口：可设为 `Important=false`，优先被裁剪

## 7. 活跃视图与调试视图

常用读取接口：
- `GetActive()`：当前推理候选上下文
- `GetArchive()`：完整历史（含已裁剪条目）
- `GetById(id)`：按 ID 查找归档条目
- `GetWindowItem(windowId)`：某窗口最新活跃条目

Server 调试端点：
- `GET /api/sessions/{id}/context`
- `GET /api/sessions/{id}/llm-input/raw`
