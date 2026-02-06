# Agent Context Interface (ACI)

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet" alt=".NET 10.0">
  <img src="https://img.shields.io/badge/C%23-13.0-239120?style=flat-square&logo=csharp" alt="C# 13.0">
  <img src="https://img.shields.io/badge/License-MIT-blue?style=flat-square" alt="MIT License">
</p>

---

## ⚠️ 免责声明

> **这是一个早期实验性版本！**
>
> 当前版本仅用于概念验证和技术探索。我们**不保证** API 的稳定性，未来可能会进行**大规模架构重构**。
>
> **请勿将本项目用于生产环境。**
>
> 如果您对这个项目感兴趣，欢迎参与讨论和贡献，但请做好随时适应变化的准备。

---

## 🤔 这是什么？

**Agent Context Interface (ACI)** 是一种**基于窗口的 AI 交互协议**，旨在解决传统对话式 AI 和 MCP 接口的局限性。
曾用名：ContextUI

在这个项目的开发过程中，我们吸取了大量前人的经验教训，如MCP，Skills等
ACI的出现不是为了取代它们，而是为了提供一种更直观、更灵活的 AI 交互方式，将Agent的能力推向更广阔的领域。

传统的 MCP（Model Context Protocol）为 AI 提供了与外部工具交互的能力。
但这些交互本质上是**抽象的函数调用**——AI 调用一个接口，获得一个结果，然后继续对话。
这种模式存在明显的局限性：

- 每次调用都是**孤立的**，缺乏持续的状态管理
- 返回的结果是**静态的**，无法反映后续的变化
- 复杂操作需要通过**大量的上下文描述**来传递状态，容易导致 Token 爆炸

**ACI 的解决思路很简单：为AI提供可以动态更新的窗口。**

窗口是一个熟悉的隐喻——它有内容、有状态、有边界、有可执行的操作。
在ACI中，窗口是一段XML文本，它可以被修改，被更新。
AI 可以"看到"窗口的当前状态，并通过操作来改变它。

以下是一个标准窗口的示例：

```xml
<Window id="todo_12345">
  <Description>待办事项管理应用</Description>
  <Content>
    item-1：买菜
    item-2：写代码
  </Content>
  <Actions>
    <action id="add" params="text:string">添加条目</action>
    <action id="delete" params="index:int">删除条目</action>
    <action id="close" params="summary:string?">关闭</action>
  </Actions>
</Window>
```

传统 MCP：   AI 调用工具 → 获得结果文本 → 结果淹没在对话历史中
ACI     ：   AI 打开窗口 → 窗口持续存在 → AI 可以随时查看和操作窗口

这种转变听起来很小，但它打开了一扇通往更复杂 AI 交互的大门。

---

## ✨ 核心理念：窗口式交互

### 传统对话式 AI 的问题

- 信息转瞬即逝，对话越长越容易丢失上下文
- AI 只能输出文字，用户需要自己理解并执行
- 每次对话都是从头开始，没有持久状态

### ACI 的解决方案

- **窗口**：信息被组织在可操作的窗口中，而非消失的聊天记录
- **操作**：AI 可以直接执行动作，而非只是告诉你怎么做
- **状态**：窗口保持持久状态，AI 可以继续之前的工作

```
┌─────────────────────────────────────────┐
│  用户：帮我管理今天的待办事项           │
│                                         │
│  AI：好的，我来打开待办管理器。         │
│                                         │
│  ┌─────────────────────────────────┐    │
│  │ 📋 待办事项                     │    │
│  │                                 │    │
│  │  ○ 买菜                         │    │
│  │  ✓ 写周报                       │    │
│  │  ○ 回复邮件                     │    │
│  │                                 │    │
│  │  [添加] [标记完成] [删除]       │    │
│  └─────────────────────────────────┘    │
│                                         │
│  用户：把"买菜"标记为完成               │
│                                         │
│  AI：已完成。                           │
│                                         │
│  ┌─────────────────────────────────┐    │
│  │ 📋 待办事项                     │    │
│  │                                 │    │
│  │  ✓ 买菜                         │    │
│  │  ✓ 写周报                       │    │
│  │  ○ 回复邮件                     │    │
│  └─────────────────────────────────┘    │
└─────────────────────────────────────────┘
```

当然，这张图片并不精确，在ACI中，窗口是动态更新的，AI每次响应时看到的窗口状态都有可能不一样。
也就是说，实际上并不会产生两个窗口，而是同一个窗口的不同状态。我们很难用图片描述这一点。

---

## 🎯 核心优势

### 1. 减少上下文污染

在传统 MCP 中，工具的返回结果会被追加到对话历史中，且**永远不会更新**。这意味着：

- 如果你查询了一个列表，后续对列表的修改不会反映在之前的查询结果中
- 对话越长，过时的信息越多，AI 越容易产生混淆

**ACI 的窗口是动态的。** 窗口的内容会随着操作实时更新，AI 在每次响应时都能看到最新的状态，而不是某个历史快照。

### 2. 持有状态

传统的工具调用是**无状态的**——每次调用都是独立的。如果你需要跨多次调用保持状态，就必须在每次调用时传递完整的上下文。

**ACI 的窗口天然拥有状态。** 应用可以在窗口中维护自己的数据，AI 的每次操作不再是孤立的：

- 上一个操作的结果可以影响下一个操作的选项
- 复杂的多步骤工作流成为可能
- AI 可以在任务中途暂停，之后继续

### 3. 可关闭与可压缩

担心 Token 爆炸？在 ACI 中，窗口可以被**关闭**。关闭时，你可以提供一个摘要，将窗口的完整状态压缩为几句话。
与此同时，ACI还有着非常智能的上下文管理机制，垃圾内容会被优先清理。

这意味着：

- 你可以编写非常详细的窗口描述和操作指南，而不必担心 Token 消耗
- 任务完成后，复杂的窗口状态可以被压缩为简洁的结论
- 上下文始终保持精简和相关

---

## 🔮 未来的可能

我们认为，ACI 最大的潜力在于**对现有程序的映射**。

想象一下：

- 将 Excel 映射为一个 ACI 窗口，AI 可以直接操作表格
- 将浏览器映射为窗口，AI 可以导航网页、填写表单
- 将 IDE 映射为窗口，AI 可以编辑代码、运行测试
- 将任何桌面软件映射为窗口，AI 可以像人类一样操作

通过这种映射，我们可以**复用互联网和软件行业数十年积累的资产**，让 AI 不必从零开始学习每一个新领域，而是站在巨人的肩膀上。

这是一个宏大的愿景，但 ACI 为此奠定了基础。

---

## ⚡ 不可忽视的挑战

### 1. Token 缓存失效

现代 LLM 服务通常会缓存 KV（Key-Value）来加速推理。但由于 ACI 中窗口的内容会动态变化，每次发送给 AI 的上下文都可能不同，这可能导致缓存命中率下降。

这是一个需要上游模型厂商配合解决的问题。理想情况下，未来的 LLM 服务可以支持"部分上下文更新"，只重新计算变化的部分。

### 2. 工具调用量增加

相比传统 MCP 的"一次调用，一个结果"模式，ACI 鼓励更频繁、更细粒度的交互。这可能导致：

- 完成同一任务需要更多的 LLM 调用
- 整体响应时间变长
- API 成本增加

但我们认为，**准确性胜过速度**。如果更多的交互能够带来更精确的执行结果和更少的错误，这种权衡是值得的。

---

## 🚀 快速开始

### 环境要求

- .NET 10.0 SDK
- OpenRouter API Key（或其他兼容的 LLM 提供商）

### 运行服务

```bash
# 克隆项目
git clone https://github.com/your-repo/AgentContextInterface.git
cd AgentContextInterface

# 配置 API Key
# 编辑 src/ACI.Server/appsettings.json
# 设置 OpenRouter.ApiKey

# 运行服务端
dotnet run --project src/ACI.Server
```

### 基本使用

```bash
# 1. 创建会话
curl -X POST http://localhost:5000/api/sessions

# 2. 与 AI 交互
curl -X POST http://localhost:5000/api/sessions/{sessionId}/interact \
  -H "Content-Type: application/json" \
  -d '{"message": "帮我创建一个待办列表"}'

# 3. 查看窗口
curl http://localhost:5000/api/sessions/{sessionId}/windows
```

---

## 📱 开发应用

ACI 允许你开发自己的"应用"——AI 可以操作的功能模块：

```csharp
public class TodoApp : ContextApp
{
    public override string Name => "todo";
    public override string? AppDescription => "管理待办事项";
    
    private List<TodoItem> _items = [];
    
    public override ContextWindow CreateWindow(string? intent)
    {
        return new ContextWindow
        {
            Description = new Text("待办事项列表，支持增删改查"),
            Content = RenderItems(),
            Actions = [
                new("add", "添加", [new("text", "string")]),
                new("toggle", "切换状态", [new("id", "int")]),
                new("delete", "删除", [new("id", "int")])
            ],
            OnAction = HandleAction
        };
    }
    
    private async Task<ActionResult> HandleAction(ActionContext ctx)
    {
        return ctx.ActionId switch
        {
            "add" => AddItem(ctx.GetString("text")),
            "toggle" => ToggleItem(ctx.GetInt("id")),
            "delete" => DeleteItem(ctx.GetInt("id")),
            _ => ActionResult.Fail("未知操作")
        };
    }
}
```

每个应用包含：

- **Name**：应用标识，用于启动
- **Description**：告诉 AI 这个应用做什么
- **CreateWindow**：创建窗口，定义内容和可用操作
- **OnAction**：处理 AI 的操作请求

详细的开发指南请参阅 [应用开发文档](./docs/tech/08-app-development.md)。

---

## 📖 文档

详细技术文档位于 [`docs/tech/`](./docs/tech/) 目录：

| 文档 | 说明 |
|------|------|
| [00-overview.md](./docs/tech/00-overview.md) | 系统架构总览 |
| [01-core-module.md](./docs/tech/01-core-module.md) | Core 模块详解 |
| [02-framework-module.md](./docs/tech/02-framework-module.md) | Framework 模块详解 |
| [03-llm-module.md](./docs/tech/03-llm-module.md) | LLM 模块详解 |
| [04-server-module.md](./docs/tech/04-server-module.md) | Server 模块详解 |
| [05-data-flow.md](./docs/tech/05-data-flow.md) | 数据流与生命周期 |
| [06-context-management.md](./docs/tech/06-context-management.md) | 上下文管理详解 |
| [07-api-reference.md](./docs/tech/07-api-reference.md) | API 参考手册 |
| [08-app-development.md](./docs/tech/08-app-development.md) | 应用开发指南 |

---

## 📊 项目状态

这是一个**早期实验性项目**，目前已完成最基本的 MVP 版本。

### 已完成

- [x] Core 模块：窗口管理、上下文管理、事件系统
- [x] Framework 模块：应用生命周期、窗口创建与刷新
- [x] LLM 模块：OpenRouter 集成、响应解析、交互控制
- [x] Server 模块：REST API、SignalR 实时通信

### 待完善

- [ ] **前端客户端**：当前前端仍处于非常基础的状态，需要大幅优化用户体验
- [ ] **异步操作支持**：目前窗口操作必须同步返回结果，AI 才能继续响应。缺少对长时间运行任务的支持
- [ ] **更多内置应用**：需要开发更多实用的示例应用
- [ ] **生产环境部署**：安全性、可靠性、可扩展性等方面需要加强

### 已知限制

- 窗口操作是同步的，无法处理需要长时间执行的任务
- 尚未实现完善的错误恢复机制
- 前端 UI 仅供演示，不适合实际使用

---

## 🤝 贡献

欢迎提出 Issue 和 Pull Request！

由于项目处于早期阶段，在贡献代码之前，建议先通过 Issue 与我们讨论你的想法，以避免重复工作或与未来的重构计划冲突。

### 贡献方向

- 🐛 Bug 修复和问题反馈
- 📖 文档改进和翻译
- 🎨 前端 UI/UX 设计与开发
- 🔧 新的内置应用开发
- 💡 架构设计讨论和建议

---

## 📄 许可证

本项目采用 [MIT License](./LICENSE) 开源。

---

<p align="center">
  <sub>Built with ❤️ and curiosity about the future of AI interaction</sub>
</p>
