# Core 模块详解（最新版）

> 对应代码：`src/ACI.Core`

## 1. 模块职责

Core 提供四类基础能力：

1. 窗口生命周期管理（`WindowManager`）
2. 上下文存储与裁剪（`ContextStore` + `ContextPruner` + `ContextManager`）
3. 动作执行与参数校验（`ActionExecutor` + `ActionParamValidator`）
4. 基础设施（`SeqClock` + `EventBus`）

## 2. 核心模型

### 2.1 Window

`Window` 是 LLM 可见的最小操作单元，关键字段如下：

- `Description` / `Content` / `Actions`
- `Options`：`Closable`、`PinInPrompt`、`Important`、`RenderMode`、`RefreshMode`
- `Meta`：`CreatedAt`、`UpdatedAt`、`Tokens`、`Hidden`

`RenderMode`：

- `Full`：输出 `meta/description/content/actions`
- `Compact`：输出紧凑文本（常用于日志窗口）

`RefreshMode`：

- `InPlace`：原地更新窗口内容
- `Append`：刷新时追加新的 `ContextItem(Window)`

### 2.2 ActionDefinition 与参数 Schema

Action 使用统一 JSON 风格 schema（`ActionParamSchema`）：

- 基础类型：`String/Integer/Number/Boolean/Null`
- 复合类型：`Object/Array`
- 可嵌套组合（`Properties` / `Items`）

动作执行模式：

- `ActionExecutionMode.Sync`（默认）
- `ActionExecutionMode.Async`（渲染为 `<action mode="async">`）

### 2.3 ContextItem

上下文条目类型：

- `System`
- `User`
- `Assistant`
- `Window`（内容存 `windowId`，渲染时动态取窗口）

附加字段：

- `Seq`：逻辑时钟序号
- `IsObsolete`：窗口关闭后的过时标记
- `EstimatedTokens`：估算 token 缓存

## 3. 上下文管理（Store + Pruner）

### 3.1 组件拆分

- `ContextStore`：线程安全存储
  - 活跃集合：`_activeItems`
  - 归档集合：`_archiveItems`
  - ID 索引：`_archiveById`
- `ContextPruner`：只负责裁剪策略
- `ContextManager`：外观层，组合 Store 与 Pruner

### 3.2 活跃与归档

- `GetActive()`：返回当前参与渲染的上下文（过滤 `IsObsolete`）
- `GetArchive()`：返回全量历史（包含被裁剪条目）
- 裁剪会真实删除活跃条目，但不删除归档备份

### 3.3 裁剪策略（当前实现）

输入参数：

- `maxTokens`
- `minConversationTokens`
- `pruneTargetTokens`

执行两阶段裁剪：

1. 优先裁剪旧 `User/Assistant`，同时优先裁剪非重要窗口（`Important=false`）
2. 仍超预算时，再裁剪旧的重要窗口（跳过 `PinInPrompt=true`）

## 4. 动作执行

`ActionExecutor.ExecuteAsync(windowId, actionId, params)` 的流程：

1. 校验窗口与 action 是否存在
2. 用 `ActionParamValidator` 校验参数
3. 执行 `IActionHandler`
4. 发布 `ActionExecutedEvent`
5. 按结果执行窗口关闭或刷新

保留动作：

- `close` 为系统保留动作，受 `window.Options.Closable` 约束

## 5. 事件体系

Core 中常见事件：

- `WindowChangedEvent`（`Created/Updated/Removed`）
- `ActionExecutedEvent`
- `AppCreatedEvent`
- `BackgroundTaskLifecycleEvent`（`Started/Completed/Failed/Canceled`）

## 6. 逻辑时钟

`SeqClock` 提供单调递增序号，用于：

- `ContextItem.Seq`
- 事件 `Seq`
- `Window.Meta.CreatedAt/UpdatedAt`

## 7. 目录结构

```text
ACI.Core/
  Abstractions/
    IActionHandler.cs
    IContextManager.cs
    IEventBus.cs
    IRenderable.cs
    ISeqClock.cs
    IWindowManager.cs
  Models/
    ActionDefinition.cs
    ActionParamSchema.cs
    ActionResult.cs
    ContextItem.cs
    Window.cs
  Services/
    ActionExecutor.cs
    ActionParamValidator.cs
    BackgroundTaskEvents.cs
    ContextManager.cs
    ContextPruner.cs
    ContextRenderer.cs
    ContextStore.cs
    EventBus.cs
    SeqClock.cs
    WindowManager.cs
```
