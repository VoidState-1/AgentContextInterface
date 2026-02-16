# 应用开发（最新版）

## 1. 最小应用骨架
```csharp
public sealed class DemoApp : ContextApp
{
    public override string Name => "demo";

    public override void OnCreate()
    {
        RegisterToolNamespace("demo",
        [
            new ToolDescriptor
            {
                Id = "echo",
                Params = new Dictionary<string, string>
                {
                    ["text"] = "string"
                },
                Description = "Echo input text."
            }
        ]);
    }

    public override ContextWindow CreateWindow(string? intent)
    {
        RegisterWindow("demo_window");

        return new ContextWindow
        {
            Id = "demo_window",
            NamespaceRefs = ["demo", "system"],
            Content = new Text("hello"),
            Actions =
            [
                new ContextAction
                {
                    Id = "echo",
                    Label = "Echo",
                    Handler = ctx => Task.FromResult(ActionResult.Ok(message: ctx.GetString("text")))
                }
            ]
        };
    }
}
```

## 2. 设计要点
- 对模型暴露：`ToolNamespaceDefinition`（`id/params/description`）。
- 对执行层落地：`ContextAction`（`Handler` + 参数校验）。
- 窗口必须通过 `NamespaceRefs` 声明可见工具。

## 3. 参数类型约定（当前）
`params` 采用简写字符串：
- 必填：`string` / `integer` / `number` / `boolean` / `object`
- 选填：在类型后加 `?`，如 `string?`
- 数组：`array<string>`

## 4. tool_call 约定
模型应输出：
```xml
<tool_call>
{"calls":[{"window_id":"demo_window","action_id":"demo.echo","params":{"text":"hi"}}]}
</tool_call>
```

## 5. 调试清单
- 窗口是否声明了正确 `NamespaceRefs`。
- 命名空间是否在 `OnCreate()` 中注册。
- 工具 `id` 是否与 `ContextAction.Id` 一致。
- 短名调用是否会产生歧义。
