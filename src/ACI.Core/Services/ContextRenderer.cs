using System.Xml.Linq;
using ACI.Core.Abstractions;
using ACI.Core.Models;

namespace ACI.Core.Services;

/// <summary>
/// LLM 消息（简化格式）。
/// </summary>
public class LlmMessage
{
    /// <summary>
    /// 角色（system / user / assistant）。
    /// </summary>
    public required string Role { get; init; }

    /// <summary>
    /// 消息内容。
    /// </summary>
    public required string Content { get; init; }
}

/// <summary>
/// 渲染选项。
/// </summary>
public class RenderOptions
{
    /// <summary>
    /// 最大 Token。
    /// </summary>
    public int MaxTokens { get; set; } = 32000;

    /// <summary>
    /// 对话保护 Token。
    /// </summary>
    public int MinConversationTokens { get; set; } = 8000;

    /// <summary>
    /// 裁剪目标 Token。
    /// </summary>
    public int PruneTargetTokens { get; set; } = 16000;
}

/// <summary>
/// 上下文渲染器接口。
/// </summary>
public interface IContextRenderer
{
    /// <summary>
    /// 将上下文项渲染为 LLM 消息列表。
    /// </summary>
    IReadOnlyList<LlmMessage> Render(
        IReadOnlyList<ContextItem> items,
        IWindowManager windowManager,
        IActionNamespaceRegistry? actionNamespaces = null,
        RenderOptions? options = null);
}

/// <summary>
/// 上下文渲染器实现。
/// </summary>
public class ContextRenderer : IContextRenderer
{
    /// <summary>
    /// 渲染上下文。
    /// </summary>
    public IReadOnlyList<LlmMessage> Render(
        IReadOnlyList<ContextItem> items,
        IWindowManager windowManager,
        IActionNamespaceRegistry? actionNamespaces = null,
        RenderOptions? options = null)
    {
        _ = options;
        var messages = new List<LlmMessage>();

        // 1. 计算当前活跃窗口引用到的命名空间消息（去重）。
        var namespaceMessages = BuildNamespaceMessages(items, windowManager, actionNamespaces);
        var namespacesInjected = namespaceMessages.Count == 0;

        // 2. 按上下文顺序渲染，首次遇到窗口前注入命名空间定义。
        foreach (var item in items)
        {
            if (item.Type == ContextItemType.Window && !namespacesInjected)
            {
                messages.AddRange(namespaceMessages);
                namespacesInjected = true;
            }

            var message = RenderItem(item, windowManager);
            if (message != null)
            {
                messages.Add(message);
            }
        }

        // 3. 兜底：若没有窗口消息但仍有命名空间定义，则追加在末尾。
        if (!namespacesInjected)
        {
            messages.AddRange(namespaceMessages);
        }

        return messages;
    }

    /// <summary>
    /// 渲染单个上下文项。
    /// </summary>
    private LlmMessage? RenderItem(ContextItem item, IWindowManager windowManager)
    {
        return item.Type switch
        {
            ContextItemType.System => new LlmMessage
            {
                Role = "system",
                Content = item.Content
            },
            ContextItemType.User => new LlmMessage
            {
                Role = "user",
                Content = item.Content
            },
            ContextItemType.Assistant => new LlmMessage
            {
                Role = "assistant",
                Content = item.Content
            },
            ContextItemType.Window => RenderWindowItem(item, windowManager),
            _ => null
        };
    }

    /// <summary>
    /// 渲染窗口项（从 WindowManager 取最新窗口内容）。
    /// </summary>
    private static LlmMessage? RenderWindowItem(ContextItem item, IWindowManager windowManager)
    {
        var windowId = item.Content;
        var window = windowManager.Get(windowId);

        if (window == null)
        {
            return null;
        }

        return new LlmMessage
        {
            Role = "user",
            Content = window.Render()
        };
    }

    /// <summary>
    /// 构建当前渲染周期需要注入的命名空间消息。
    /// </summary>
    private static List<LlmMessage> BuildNamespaceMessages(
        IReadOnlyList<ContextItem> items,
        IWindowManager windowManager,
        IActionNamespaceRegistry? actionNamespaces)
    {
        if (actionNamespaces == null)
        {
            return [];
        }

        var namespaceIds = items
            .Where(i => i.Type == ContextItemType.Window)
            .Select(i => windowManager.Get(i.Content))
            .Where(w => w != null)
            .SelectMany(w => w!.NamespaceRefs)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (namespaceIds.Count == 0)
        {
            return [];
        }

        var definitions = actionNamespaces.GetByIds(namespaceIds)
            .OrderBy(n => n.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var messages = new List<LlmMessage>(definitions.Count);
        foreach (var definition in definitions)
        {
            messages.Add(new LlmMessage
            {
                Role = "user",
                Content = RenderNamespaceDefinition(definition)
            });
        }

        return messages;
    }

    /// <summary>
    /// 渲染单个命名空间定义。
    /// </summary>
    private static string RenderNamespaceDefinition(ActionNamespaceDefinition definition)
    {
        var xml = new XElement(
            "namespace",
            new XAttribute("id", definition.Id),
            new XCData(definition.RenderPromptJson()));

        return xml.ToString(SaveOptions.DisableFormatting);
    }
}
