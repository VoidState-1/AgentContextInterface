# 上下文管理详解（最新版）

> 对应实现：`ContextStore` + `ContextPruner` + `ContextRenderer`

## 1. 设计目标

当前上下文系统追求三件事：

1. 保证 LLM 可见上下文始终反映窗口最新状态
2. 裁剪时真正缩减活跃上下文长度
3. 保留归档历史供调试与后续检索

## 2. 数据结构

### 2.1 ContextItem

核心字段：

- `Id`
- `Type`（`System/User/Assistant/Window`）
- `Seq`
- `Content`
- `IsObsolete`
- `EstimatedTokens`

`Window` 类型仅存 `windowId`，渲染时动态读取窗口内容。

### 2.2 双视图存储

`ContextStore` 内部维护：

- `activeItems`：参与渲染与裁剪
- `archiveItems`：完整备份
- `archiveById`：按 ID 索引

`Prune` 只会修改 `activeItems`，不会删除 `archiveItems`。

## 3. 写入与过时标记

### 3.1 Add

添加条目时：

1. 分配 `Seq = clock.Next()`
2. 估算 token
3. 若是 `Window` 条目，先把同窗口旧活跃条目标记为 `IsObsolete=true`
4. 同时写入活跃与归档

### 3.2 MarkWindowObsolete

当窗口关闭时，关联的窗口上下文条目会标记过时。

## 4. 渲染规则

`ContextRenderer.Render(items, windowManager, options)`：

- `System` → `role=system`
- `User` → `role=user`
- `Assistant` → `role=assistant`
- `Window` → 从 `windowManager.Get(windowId)` 取最新 `window.Render()`，作为 `role=user`

若窗口不存在（已关闭），该条目跳过。

## 5. 裁剪规则

输入：

- `maxTokens`
- `minConversationTokens`
- `pruneTargetTokens`（<=0 时回退到 `maxTokens/2`）

### 5.1 总体流程

1. 计算活跃条目估算 token 总量
2. 若未超 `maxTokens`，不裁剪
3. 超限后先裁到 `pruneTargetTokens`

### 5.2 两阶段策略

阶段一：

- 优先裁剪旧对话（`User/Assistant`）
- 但对话 token 不能低于 `minConversationTokens`
- 同时优先裁剪非重要窗口（`Important=false`，且非 `PinInPrompt`）

阶段二：

- 若仍超限，继续裁剪旧的重要窗口
- `PinInPrompt=true` 的窗口不参与裁剪

## 6. 重要窗口与固定窗口

来自 `WindowOptions`：

- `Important`（默认 `true`）：参与裁剪优先级
- `PinInPrompt`（默认 `false`）：始终保留在提示词中

典型用法：

- `launcher`：`PinInPrompt=true`，确保 AI 随时可启动应用
- 临时日志窗口可设为 `Important=false`，优先被裁剪

## 7. 活跃视图与调试视图

常用读取接口：

- `GetActive()`：实际发给 LLM 的候选上下文
- `GetArchive()`：完整历史（含被裁剪条目）
- `GetById(id)`：从归档检索单条
- `GetWindowItem(windowId)`：窗口最新活跃条目

Server 端提供调试接口：

- `GET /api/sessions/{id}/context`
- `GET /api/sessions/{id}/context/raw`
- `GET /api/sessions/{id}/llm-input/raw`
