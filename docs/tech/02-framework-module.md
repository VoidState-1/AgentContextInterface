# Framework 模块详解（最新版）

> 对应代码：`src/ACI.Framework`

## 1. 模块职责

Framework 负责把“应用定义”转成“可被 Core/LLM 操作的窗口”：

1. 提供应用基类与运行时上下文（`ContextApp`、`IContext`、`RuntimeContext`）
2. 管理应用生命周期与窗口刷新（`FrameworkHost`）
3. 提供参数 schema DSL（`Param`）
4. 内置应用（`launcher`、`activity_log`、`file_explorer`）

## 2. RuntimeContext（IContext 实现）

`IContext` 暴露以下能力：

- Core 服务：`Windows`、`Events`、`Clock`、`Context`
- UI 刷新：`RequestRefresh(windowId)`
- 后台任务：`StartBackgroundTask`、`CancelBackgroundTask`
- 会话串行回写：`RunOnSessionAsync`
- 服务定位：`GetService<T>()`

`RuntimeContext` 由 `SessionContext` 注入后台任务处理器，由 `FrameworkHost` 注入刷新处理器。

## 3. ContextApp 生命周期

`ContextApp` 是应用基类，主流程如下：

1. `FrameworkHost.Register(app)`
2. 首次启动时 `Initialize(state, context)` + `OnCreate()`
3. `CreateWindow(intent)` 生成主窗口
4. `RefreshWindow(windowId, intent)` 刷新（默认回退到 `CreateWindow`）
5. 关闭应用时 `OnDestroy()`

每个应用有独立 `IAppState`（当前实现：`InMemoryAppState`）。

## 4. ContextAction 与参数声明

`ContextAction` 字段：

- `Id`
- `Label`
- `Handler`
- `Params`（`ActionParamSchema`）
- `Mode`（`Sync/Async`）

当 `Mode=Async` 时，渲染到窗口 XML 会带 `mode="async"`，LLM 无需手填执行模式。

参数声明推荐用 `Param` DSL：

```csharp
Params = Param.Object(new()
{
    ["path"] = Param.String(),
    ["options"] = Param.Object(new()
    {
        ["recursive"] = Param.Boolean(required: false, defaultValue: false),
        ["filters"] = Param.Array(Param.String(), required: false)
    }, required: false)
})
```

## 5. FrameworkHost 关键行为

### 5.1 启动应用

`Launch(appName, intent)`：

1. 确保应用已启动生命周期（`EnsureStarted`）
2. 调用应用 `CreateWindow`
3. 转为 Core `Window` 并分配 `CreatedAt/UpdatedAt`
4. 建立 `windowId -> appName/intent` 映射
5. 发布 `AppCreatedEvent`
6. 写入 `WindowManager`

### 5.2 刷新窗口

`RefreshWindow(windowId)` 原地更新：

- 保留 `CreatedAt`
- 更新 `Description/Content/Actions/Handler`
- 更新 `UpdatedAt`
- 调用 `WindowManager.NotifyUpdated`
- 发布 `WindowRefreshedEvent`

## 6. 内置应用（当前）

### 6.1 launcher（常驻启动器）

- 窗口 ID 固定：`launcher`
- `Closable=false`
- `PinInPrompt=true`
- `open` 动作返回 `data.action="launch"`，由 LLM 层统一触发 `Host.Launch(...)`

### 6.2 activity_log（活动日志）

- 订阅 `ActionExecutedEvent`、`AppCreatedEvent`、`BackgroundTaskLifecycleEvent`
- 产生日志窗口，默认 `RenderMode=Compact`
- 日志主窗口支持 `clear` / `close`

### 6.3 file_explorer（文件浏览器）

- 支持目录浏览、按索引打开、输入路径打开、返回上级、回到 home、查看驱动器
- 展示条目索引，动作参数使用 schema 声明

## 7. 目录结构

```text
ACI.Framework/
  Runtime/
    ContextApp.cs
    ContextAction.cs
    ContextWindow.cs
    FrameworkHost.cs
    IAppState.cs
    IContext.cs
    Param.cs
    RuntimeContext.cs
  Components/
    Text.cs
    HStack.cs
    VStack.cs
    Tree.cs
  BuiltIn/
    AppLauncher.cs
    ActivityLog.cs
    FileExplorerApp.cs
```
