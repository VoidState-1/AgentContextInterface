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
        var renderedNamespaceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. 按上下文顺序渲染每个条目。
        // 2. 如果当前是窗口条目，则在当前末尾补上首次出现的 namespace 定义。
        foreach (var item in items)
        {
            var message = RenderItem(item, windowManager);
            if (message != null)
            {
                messages.Add(message);
            }

            AppendNamespaceMessagesForWindow(
                messages,
                item,
                windowManager,
                actionNamespaces,
                renderedNamespaceIds);
        }

        return messages;
    }

    /// <summary>
    /// 渲染单个上下文项。
    /// </summary>
    private static LlmMessage? RenderItem(ContextItem item, IWindowManager windowManager)
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
    /// 渲染窗口项（从 WindowManager 读取最新窗口内容）。
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
    /// 按窗口引用在当前末尾追加首次出现的命名空间定义。
    /// </summary>
    private static void AppendNamespaceMessagesForWindow(
        ICollection<LlmMessage> messages,
        ContextItem item,
        IWindowManager windowManager,
        IActionNamespaceRegistry? actionNamespaces,
        ISet<string> renderedNamespaceIds)
    {
        if (item.Type != ContextItemType.Window || actionNamespaces == null)
        {
            return;
        }

        var window = windowManager.Get(item.Content);
        if (window == null)
        {
            return;
        }

        foreach (var namespaceId in window.NamespaceRefs.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            if (renderedNamespaceIds.Contains(namespaceId))
            {
                continue;
            }

            if (!actionNamespaces.TryGet(namespaceId, out var definition) || definition == null)
            {
                continue;
            }

            messages.Add(new LlmMessage
            {
                Role = "user",
                Content = RenderNamespaceDefinition(definition)
            });

            renderedNamespaceIds.Add(definition.Id);
        }
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
