# 应用开发指南（最新版）

> 本文档对应当前 `ContextApp` / `ContextWindow` / `ContextAction` 实现。

## 1. 最小可运行应用

```csharp
using ACI.Framework.Runtime;
using ACI.Framework.Components;

public sealed class HelloApp : ContextApp
{
    public override string Name => "hello";
    public override string? AppDescription => "Simple hello demo.";

    public override ContextWindow CreateWindow(string? intent)
    {
        return new ContextWindow
        {
            Id = "hello_main",
            Description = new Text("A minimal app."),
            Content = new Text("Hello, ACI."),
            Actions =
            [
                new ContextAction
                {
                    Id = "close",
                    Label = "Close",
                    Handler = _ => Task.FromResult(ACI.Core.Models.ActionResult.Close())
                }
            ]
        };
    }
}
```

注册方式（会话初始化阶段）：

```csharp
host.Register(new HelloApp());
```

## 2. 推荐开发模型

1. `ContextApp` 持有状态（`State` / 私有字段）
2. `CreateWindow` 输出当前状态快照
3. action 修改状态并返回 `ActionResult`
4. 需要重绘时返回 `shouldRefresh: true`

## 3. 参数定义（JSON Schema DSL）

当前应使用 `Param` 构造参数结构，支持完整 JSON 类型：

- `Param.String(...)`
- `Param.Integer(...)`
- `Param.Number(...)`
- `Param.Boolean(...)`
- `Param.Null(...)`
- `Param.Object(properties, ...)`
- `Param.Array(items, ...)`

示例：

```csharp
Params = Param.Object(new()
{
    ["query"] = Param.String(),
    ["limit"] = Param.Integer(required: false, defaultValue: 20),
    ["filters"] = Param.Object(new()
    {
        ["ext"] = Param.Array(Param.String(), required: false),
        ["include_hidden"] = Param.Boolean(required: false, defaultValue: false)
    }, required: false)
})
```

## 4. 读取参数

`ActionContext` 提供：

- `GetString(name)`
- `GetInt(name)`
- `GetBool(name, defaultValue)`
- `GetValue(name)`（原始 `JsonElement`）
- `GetAs<T>(name)`（反序列化）

示例：

```csharp
var path = ctx.GetString("path");
var options = ctx.GetAs<MyOptions>("options");
```

## 5. 异步动作（不阻塞对话）

把 action 标记为异步：

```csharp
new ContextAction
{
    Id = "scan",
    Label = "Scan",
    Handler = HandleScanAsync
}.AsAsync()
```

行为：

- 模型触发该 action 后，系统立即返回 `task_id`
- 任务在后台执行
- 可通过 `Context.StartBackgroundTask` / `CancelBackgroundTask` 自行管理任务
- 若后台任务要改会话状态，使用 `RunOnSessionAsync`

## 6. ActionResult 约定

- `ActionResult.Ok(...)`：成功
- `ActionResult.Fail(message)`：失败
- `ActionResult.Close(summary)`：关闭窗口

常用字段：

- `message`：即时反馈
- `summary`：摘要日志
- `shouldRefresh`：是否刷新窗口
- `shouldClose`：是否关闭窗口
- `data`：扩展数据（例如 `launcher` 使用 `data.action="launch"`）

## 7. 窗口选项建议

`WindowOptions` 常用配置：

- `Closable=false`：不可关闭
- `PinInPrompt=true`：裁剪时固定保留
- `Important=false`：裁剪时优先移除
- `RenderMode=Compact`：紧凑输出
- `RefreshMode=Append`：刷新时追加上下文窗口项

## 8. 命名与描述建议

- 应用名：小写 + 下划线（如 `file_explorer`）
- action id：短小明确（如 `open_path`、`refresh`）
- Description：写清窗口用途、可用 action、关键参数

## 9. 调试建议

优先使用后端调试端点观察行为：

- `/api/sessions/{id}/apps`
- `/api/sessions/{id}/windows`
- `/api/sessions/{id}/context`
- `/api/sessions/{id}/llm-input/raw`
- `/api/sessions/{id}/interact/simulate`

这样可以直接核对：

- action schema 是否正确渲染到窗口 XML
- tool_call 是否按预期被解析
- 裁剪后上下文是否保留了关键窗口
