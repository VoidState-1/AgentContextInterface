# 数据流与生命周期

> 本文档详细描述 ACI 系统中的数据流动和关键对象的生命周期。

## 1. 用户交互完整流程

### 1.1 时序图

```mermaid
sequenceDiagram
    autonumber
    participant Client as 客户端
    participant API as REST API
    participant SM as SessionManager
    participant SC as SessionContext
    participant IC as InteractionController
    participant CM as ContextManager
    participant CR as ContextRenderer
    participant LLM as OpenRouterClient
    participant AP as ActionParser
    participant FH as FrameworkHost
    participant App as ContextApp
    participant WM as WindowManager
    
    Client->>API: POST /interact {message}
    API->>SM: GetSession(sessionId)
    SM-->>API: SessionContext
    API->>SC: Interaction.ProcessAsync(message)
    
    Note over IC: 初始化检查
    IC->>CM: Add(SystemPrompt)
    
    Note over IC: 添加用户消息
    IC->>CM: Add(UserMessage)
    
    Note over IC: 渲染上下文
    IC->>CM: GetActive()
    CM-->>IC: ContextItem[]
    IC->>CR: Render(items, windows)
    CR->>WM: Get(windowId) for each window
    WM-->>CR: Window
    CR-->>IC: LlmMessage[]
    
    Note over IC: 调用 LLM
    IC->>LLM: SendAsync(messages)
    LLM-->>IC: LLMResponse
    
    Note over IC: 记录 AI 响应
    IC->>CM: Add(AssistantMessage)
    
    Note over IC: 解析操作
    IC->>AP: Parse(response)
    AP-->>IC: ParsedAction
    
    alt create 操作
        IC->>FH: Launch(appName, intent)
        FH->>App: CreateWindow(intent)
        App-->>FH: ContextWindow
        FH->>WM: Add(window)
        IC->>CM: Add(WindowItem)
    else action 操作
        IC->>WM: Get(windowId)
        WM-->>IC: Window
        IC->>App: ExecuteAsync(context)
        App-->>IC: ActionResult
        opt ShouldRefresh
            IC->>FH: RefreshWindow(windowId)
        end
        opt ShouldClose
            IC->>WM: Remove(windowId)
            IC->>CM: MarkWindowObsolete(windowId)
        end
    end
    
    IC-->>API: InteractionResult
    API-->>Client: Response
```

## 2. 窗口生命周期

### 2.1 状态图

```mermaid
stateDiagram-v2
    [*] --> Created: FrameworkHost.Launch()
    Created --> Active: WindowManager.Add()
    
    Active --> Refreshing: RefreshWindow()
    Refreshing --> Active: 更新完成
    
    Active --> Closed: Remove() / close action
    Closed --> [*]
    
    note right of Active
        窗口处于活跃状态
        可被 AI 查看和操作
    end note
    
    note right of Closed
        窗口已关闭
        ContextItem 标记为 Obsolete
    end note
```

### 2.2 创建流程

```mermaid
flowchart TD
    START[开始]
    CREATE[ContextApp.CreateWindow]
    CONVERT[转换为 Window]
    HANDLER[设置 ActionHandler]
    REGISTER[注册到 WindowManager]
    CONTEXT[添加到 ContextManager]
    EVENT[发布 AppCreatedEvent]
    END[结束]
    
    START --> CREATE
    CREATE --> CONVERT
    CONVERT --> HANDLER
    HANDLER --> REGISTER
    REGISTER --> CONTEXT
    CONTEXT --> EVENT
    EVENT --> END
```

### 2.3 刷新机制

```mermaid
flowchart TD
    START[RequestRefresh]
    FIND[查找窗口和应用]
    REFRESH[App.RefreshWindow]
    UPDATE[更新 Content 和 Actions]
    SEQ[更新 UpdatedAt]
    NOTIFY[NotifyUpdated]
    EVENT[发布 WindowRefreshedEvent]
    END[结束]
    
    START --> FIND
    FIND --> REFRESH
    REFRESH --> UPDATE
    UPDATE --> SEQ
    SEQ --> NOTIFY
    NOTIFY --> EVENT
    EVENT --> END
```

关键特性：
- **窗口 ID 不变**：保持在上下文中的位置
- **原地更新**：只更新内容，不重新创建
- **UpdatedAt 更新**：使用 `Clock.Next()` 获取新时间戳

### 2.4 关闭流程

```mermaid
flowchart TD
    START[close action]
    REMOVE[WindowManager.Remove]
    MARK[ContextManager.MarkObsolete]
    EVENT[发布 WindowChangedEvent]
    RESULT[返回 ActionResult.Close]
    END[结束]
    
    START --> REMOVE
    REMOVE --> MARK
    MARK --> EVENT
    EVENT --> RESULT
    RESULT --> END
```

## 3. 上下文项生命周期

### 3.1 类型说明

| 类型 | 来源 | 生命周期 | 说明 |
|------|------|----------|------|
| System | 系统初始化 | 永久 | 系统提示词，永不删除 |
| User | 用户输入 | 可裁剪 | 用户消息 |
| Assistant | AI 响应 | 可裁剪 | AI 回复 |
| Window | 窗口创建 | 随窗口 | 关闭时标记过期 |

### 3.2 状态转换

```mermaid
stateDiagram-v2
    [*] --> Active: Add()
    Active --> Active: 渲染时获取最新内容
    Active --> Obsolete: MarkWindowObsolete()
    Active --> Trimmed: 上下文裁剪
    Obsolete --> Trimmed: 优先被裁剪
    Trimmed --> [*]
```

## 4. 会话生命周期

### 4.1 状态图

```mermaid
stateDiagram-v2
    [*] --> Created: CreateSession()
    Created --> Ready: 服务初始化完成
    Ready --> Interacting: ProcessAsync()
    Interacting --> Ready: 交互完成
    Ready --> Disposed: CloseSession()
    Disposed --> [*]
```

### 4.2 会话包含的资源

```mermaid
graph TB
    subgraph "SessionContext"
        subgraph "Core"
            Clock[SeqClock]
            Events[EventBus]
            Windows[WindowManager]
            Context[ContextManager]
        end
        
        subgraph "Framework"
            Runtime[RuntimeContext]
            Host[FrameworkHost]
            Apps[Registered Apps]
        end
        
        subgraph "LLM"
            Interaction[InteractionController]
        end
    end
```

## 5. 事件流

### 5.1 事件发布点

```mermaid
flowchart TB
    subgraph "FrameworkHost"
        LAUNCH["Launch()<br/>→ AppCreatedEvent"]
        REFRESH["RefreshWindow()<br/>→ WindowRefreshedEvent"]
        EXEC["ExecuteActionAsync()<br/>→ ActionExecutedEvent"]
    end
    
    subgraph "WindowManager"
        ADD["Add()<br/>→ WindowChangedEvent(Created)"]
        REMOVE["Remove()<br/>→ WindowChangedEvent(Removed)"]
        UPDATE["NotifyUpdated()<br/>→ WindowChangedEvent(Updated)"]
    end
```

### 5.2 事件订阅示例

```csharp
// 订阅窗口变化
context.Events.Subscribe<WindowChangedEvent>(e =>
{
    switch (e.Type)
    {
        case WindowEventType.Created:
            Console.WriteLine($"窗口创建: {e.WindowId}");
            break;
        case WindowEventType.Updated:
            Console.WriteLine($"窗口更新: {e.WindowId}");
            break;
        case WindowEventType.Removed:
            Console.WriteLine($"窗口关闭: {e.WindowId}");
            break;
    }
});

// 订阅操作执行
context.Events.Subscribe<ActionExecutedEvent>(e =>
{
    Console.WriteLine($"操作执行: {e.ActionId} on {e.WindowId}");
});
```

## 6. Seq 分配流程

### 6.1 分配时机

```mermaid
flowchart LR
    subgraph "ContextManager"
        ADD["Add(item)<br/>item.Seq = clock.Next()"]
    end
    
    subgraph "FrameworkHost"
        EVENT1["AppCreatedEvent<br/>Seq = clock.Next()"]
        EVENT2["ActionExecutedEvent<br/>Seq = clock.Next()"]
        EVENT3["WindowRefreshedEvent<br/>Seq = clock.Next()"]
    end
    
    subgraph "WindowManager"
        WINDOW["Window.Meta<br/>UpdatedAt = clock.Next()"]
    end
```

### 6.2 Seq 用途

| 用途 | 场景 | 意义 |
|------|------|------|
| 排序 | ContextItem.Seq | 确定上下文中的顺序 |
| 时间戳 | Event.Seq | 标记事件发生时间 |
| 版本 | Window.Meta.UpdatedAt | 追踪窗口更新 |

## 7. 数据一致性

### 7.1 单线程模型

每个 `SessionContext` 内部的操作是**同步**的：

```mermaid
flowchart LR
    R1[请求 1] --> Q[队列]
    R2[请求 2] --> Q
    R3[请求 3] --> Q
    Q --> P[处理器]
    P --> S[SessionContext]
```

### 7.2 窗口引用一致性

`ContextItem` 存储的是窗口 **ID**，渲染时动态获取最新内容：

```csharp
// ContextItem 存储
new ContextItem
{
    Type = ContextItemType.Window,
    Content = windowId  // 只存储 ID
};

// 渲染时获取
var window = windowManager.Get(windowId);
if (window != null)
{
    return new LlmMessage { Content = window.Render() };
}
```

这确保了：
- 窗口刷新后，上下文自动反映最新状态
- 窗口关闭后，渲染返回 null，跳过该项
