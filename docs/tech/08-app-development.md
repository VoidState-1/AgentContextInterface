# 应用开发指南

> 本文档指导如何在 ACI 框架中开发自定义应用。

## 1. 概述

ACI 应用是封装特定功能的独立单元。每个应用可以：

- 创建和管理自己的窗口
- 定义窗口内容和可用操作
- 处理用户/AI 的操作请求
- 维护内部状态

## 2. 快速开始

### 2.1 最简应用

```csharp
public class HelloApp : ContextApp
{
    public override string Name => "hello";
    
    public override ContextWindow CreateWindow(string? intent)
    {
        return new ContextWindow
        {
            Content = new Text("Hello, ACI!")
        };
    }
}
```

### 2.2 注册应用

```csharp
// 在 SessionContext 或 FrameworkHost 中注册
host.Register(new HelloApp());
```

## 3. 应用基类 API

### 3.1 属性

```csharp
public abstract class ContextApp
{
    /// <summary>
    /// 应用名称（必需，用于启动命令）
    /// </summary>
    public abstract string Name { get; }
    
    /// <summary>
    /// 应用描述（可选，显示在应用列表）
    /// </summary>
    public virtual string? AppDescription => null;
    
    /// <summary>
    /// 标签（可选，用于分类和搜索）
    /// </summary>
    public virtual string[] Tags => [];
}
```

### 3.2 生命周期方法

```csharp
/// <summary>
/// 初始化（注册时调用）
/// </summary>
public virtual void Initialize(IContext context) { }

/// <summary>
/// 销毁（会话关闭时调用）
/// </summary>
public virtual void Dispose() { }
```

### 3.3 窗口方法

```csharp
/// <summary>
/// 创建窗口（必须实现）
/// </summary>
public abstract ContextWindow CreateWindow(string? intent);

/// <summary>
/// 刷新窗口（可选，默认调用 CreateWindow）
/// </summary>
public virtual ContextWindow RefreshWindow(string windowId, string? intent = null)
    => CreateWindow(intent);
```

## 4. 窗口定义

### 4.1 ContextWindow 结构

```csharp
public class ContextWindow
{
    /// <summary>
    /// 窗口 ID（可选，默认自动生成）
    /// </summary>
    public string? Id { get; init; }
    
    /// <summary>
    /// 描述（告诉 AI 这是什么）
    /// </summary>
    public IRenderable? Description { get; init; }
    
    /// <summary>
    /// 内容（必需）
    /// </summary>
    public required IRenderable Content { get; init; }
    
    /// <summary>
    /// 可用操作
    /// </summary>
    public List<ActionDefinition> Actions { get; init; } = [];
    
    /// <summary>
    /// 窗口选项
    /// </summary>
    public WindowOptions? Options { get; init; }
    
    /// <summary>
    /// 操作处理器
    /// </summary>
    public Func<ActionContext, Task<ActionResult>>? OnAction { get; init; }
}
```

### 4.2 定义操作

```csharp
// 无参数操作
new ActionDefinition("refresh", "刷新")

// 带参数操作
new ActionDefinition("add", "添加", [
    new ParameterDefinition { Name = "text", Type = "string", Required = true }
])

// 可选参数
new ActionDefinition("search", "搜索", [
    new ParameterDefinition { Name = "query", Type = "string", Required = true },
    new ParameterDefinition { Name = "limit", Type = "int", Required = false, Default = 10 }
])
```

### 4.3 参数类型

| 类型 | 说明 | 示例 |
|------|------|------|
| `string` | 字符串 | "hello" |
| `int` | 整数 | 42 |
| `bool` | 布尔值 | true |
| `float` | 浮点数 | 3.14 |

## 5. 处理操作

### 5.1 操作处理器

```csharp
public override ContextWindow CreateWindow(string? intent)
{
    return new ContextWindow
    {
        Content = RenderContent(),
        Actions = [
            new("add", "添加", [new("text", "string")]),
            new("delete", "删除", [new("index", "int")])
        ],
        OnAction = HandleAction
    };
}

private async Task<ActionResult> HandleAction(ActionContext ctx)
{
    return ctx.ActionId switch
    {
        "add" => await HandleAdd(ctx),
        "delete" => await HandleDelete(ctx),
        _ => ActionResult.Fail($"未知操作: {ctx.ActionId}")
    };
}
```

### 5.2 获取参数

```csharp
private Task<ActionResult> HandleAdd(ActionContext ctx)
{
    // 获取字符串参数
    var text = ctx.GetString("text");
    if (string.IsNullOrEmpty(text))
        return Task.FromResult(ActionResult.Fail("text 参数不能为空"));
    
    _items.Add(text);
    
    // 返回成功并请求刷新
    return Task.FromResult(ActionResult.Ok(
        message: $"已添加: {text}",
        shouldRefresh: true
    ));
}

private Task<ActionResult> HandleDelete(ActionContext ctx)
{
    // 获取整数参数
    var index = ctx.GetInt("index");
    if (index == null || index < 0 || index >= _items.Count)
        return Task.FromResult(ActionResult.Fail("无效的索引"));
    
    var removed = _items[index.Value];
    _items.RemoveAt(index.Value);
    
    return Task.FromResult(ActionResult.Ok(
        message: $"已删除: {removed}",
        shouldRefresh: true
    ));
}
```

### 5.3 ActionResult 选项

```csharp
// 成功
ActionResult.Ok(message: "操作成功")

// 成功并刷新窗口
ActionResult.Ok(message: "已添加", shouldRefresh: true)

// 失败
ActionResult.Fail("操作失败的原因")

// 关闭窗口
ActionResult.Close(summary: "完成了 3 个任务")
```

## 6. UI 组件

### 6.1 Text

简单文本。

```csharp
new Text("Hello World")
new Text("标题", tag: "h1")
new Text("段落内容", tag: "p")
```

### 6.2 Column

垂直列表。

```csharp
new Column([
    new Text("项目 1"),
    new Text("项目 2"),
    new Text("项目 3")
])

// 自定义标签
new Column(items, itemTag: "li")
```

### 6.3 Row

水平排列。

```csharp
new Row([
    new Text("A"),
    new Text("B"),
    new Text("C")
])

// 自定义分隔符
new Row(items, separator: " | ")
```

### 6.4 自定义组件

实现 `IRenderable` 接口：

```csharp
public class Table : IRenderable
{
    private readonly string[] _headers;
    private readonly string[][] _rows;
    
    public Table(string[] headers, string[][] rows)
    {
        _headers = headers;
        _rows = rows;
    }
    
    public XElement ToXml()
    {
        var table = new XElement("table");
        
        // 表头
        var header = new XElement("thead");
        foreach (var h in _headers)
            header.Add(new XElement("th", h));
        table.Add(header);
        
        // 数据行
        var body = new XElement("tbody");
        foreach (var row in _rows)
        {
            var tr = new XElement("tr");
            foreach (var cell in row)
                tr.Add(new XElement("td", cell));
            body.Add(tr);
        }
        table.Add(body);
        
        return table;
    }
    
    public string Render() => ToXml().ToString();
}
```

## 7. 完整示例

### 7.1 待办事项应用

```csharp
public class TodoApp : ContextApp
{
    public override string Name => "todo";
    public override string? AppDescription => "管理待办事项，支持增删查";
    public override string[] Tags => ["productivity", "list"];
    
    private readonly List<TodoItem> _items = [];
    private int _nextId = 1;
    
    public override ContextWindow CreateWindow(string? intent)
    {
        return new ContextWindow
        {
            Description = new Text("待办事项列表。使用 add 添加新条目，toggle 切换完成状态，delete 删除条目。"),
            Content = RenderList(),
            Actions = [
                new("add", "添加条目", [
                    new ParameterDefinition { Name = "text", Type = "string", Required = true }
                ]),
                new("toggle", "切换状态", [
                    new ParameterDefinition { Name = "id", Type = "int", Required = true }
                ]),
                new("delete", "删除条目", [
                    new ParameterDefinition { Name = "id", Type = "int", Required = true }
                ]),
                new("clear", "清空已完成")
            ],
            OnAction = HandleAction
        };
    }
    
    private IRenderable RenderList()
    {
        if (_items.Count == 0)
            return new Text("(空列表)");
        
        return new Column(
            _items.Select(item => 
                new Text($"[{item.Id}] {(item.Done ? "✓" : "○")} {item.Text}")
            ).ToArray()
        );
    }
    
    private Task<ActionResult> HandleAction(ActionContext ctx)
    {
        return ctx.ActionId switch
        {
            "add" => Add(ctx.GetString("text")),
            "toggle" => Toggle(ctx.GetInt("id")),
            "delete" => Delete(ctx.GetInt("id")),
            "clear" => ClearCompleted(),
            _ => Task.FromResult(ActionResult.Fail($"未知操作: {ctx.ActionId}"))
        };
    }
    
    private Task<ActionResult> Add(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Task.FromResult(ActionResult.Fail("请提供待办内容"));
        
        _items.Add(new TodoItem { Id = _nextId++, Text = text });
        
        return Task.FromResult(ActionResult.Ok(
            message: $"已添加: {text}",
            shouldRefresh: true
        ));
    }
    
    private Task<ActionResult> Toggle(int? id)
    {
        var item = _items.FirstOrDefault(i => i.Id == id);
        if (item == null)
            return Task.FromResult(ActionResult.Fail($"找不到 ID={id} 的条目"));
        
        item.Done = !item.Done;
        
        return Task.FromResult(ActionResult.Ok(
            message: $"已{(item.Done ? "完成" : "取消完成")}: {item.Text}",
            shouldRefresh: true
        ));
    }
    
    private Task<ActionResult> Delete(int? id)
    {
        var item = _items.FirstOrDefault(i => i.Id == id);
        if (item == null)
            return Task.FromResult(ActionResult.Fail($"找不到 ID={id} 的条目"));
        
        _items.Remove(item);
        
        return Task.FromResult(ActionResult.Ok(
            message: $"已删除: {item.Text}",
            shouldRefresh: true
        ));
    }
    
    private Task<ActionResult> ClearCompleted()
    {
        var count = _items.RemoveAll(i => i.Done);
        
        return Task.FromResult(ActionResult.Ok(
            message: $"已清空 {count} 个已完成条目",
            shouldRefresh: true
        ));
    }
    
    private class TodoItem
    {
        public int Id { get; init; }
        public required string Text { get; init; }
        public bool Done { get; set; }
    }
}
```

## 8. 最佳实践

### 8.1 命名规范

| 项目 | 规范 | 示例 |
|------|------|------|
| 应用名 | 小写，下划线分隔 | `todo`, `file_manager` |
| 操作 ID | 小写，下划线分隔 | `add`, `delete_all` |
| 参数名 | 小写，下划线分隔 | `item_id`, `search_query` |

### 8.2 描述编写

好的描述帮助 AI 理解如何使用应用：

```csharp
// ❌ 不好
Description = new Text("待办列表")

// ✓ 好
Description = new Text("待办事项管理应用。显示当前所有待办条目，支持添加新条目(add)、标记完成(toggle)和删除(delete)。")
```

### 8.3 错误处理

```csharp
private Task<ActionResult> HandleAction(ActionContext ctx)
{
    try
    {
        // 验证必需参数
        var text = ctx.GetString("text");
        if (string.IsNullOrEmpty(text))
            return Task.FromResult(ActionResult.Fail("text 参数不能为空"));
        
        // 执行操作...
        
        return Task.FromResult(ActionResult.Ok("成功", shouldRefresh: true));
    }
    catch (Exception ex)
    {
        return Task.FromResult(ActionResult.Fail($"操作失败: {ex.Message}"));
    }
}
```

### 8.4 状态管理

```csharp
public class StatefulApp : ContextApp
{
    // 应用级状态：所有窗口共享
    private List<string> _sharedItems = [];
    
    // 窗口级状态：每个窗口独立
    private Dictionary<string, WindowState> _windowStates = [];
    
    public override ContextWindow CreateWindow(string? intent)
    {
        var windowId = Guid.NewGuid().ToString();
        _windowStates[windowId] = new WindowState();
        
        return new ContextWindow
        {
            Id = windowId,
            Content = Render(windowId),
            OnAction = ctx => HandleAction(ctx, ctx.Window.Id)
        };
    }
}
```

## 9. 调试技巧

### 9.1 日志输出

```csharp
public override ContextWindow CreateWindow(string? intent)
{
    Console.WriteLine($"[{Name}] CreateWindow called, intent: {intent}");
    // ...
}

private Task<ActionResult> HandleAction(ActionContext ctx)
{
    Console.WriteLine($"[{Name}] Action: {ctx.ActionId}, Params: {JsonSerializer.Serialize(ctx.Parameters)}");
    // ...
}
```

### 9.2 测试应用

```csharp
[Test]
public async Task TodoApp_AddItem_ShouldSucceed()
{
    var app = new TodoApp();
    var window = app.CreateWindow(null);
    
    var result = await window.OnAction!(new ActionContext
    {
        Window = new Window { Id = "test" },
        ActionId = "add",
        Parameters = new() { ["text"] = "买菜" }
    });
    
    Assert.True(result.Success);
    Assert.True(result.ShouldRefresh);
}
```
