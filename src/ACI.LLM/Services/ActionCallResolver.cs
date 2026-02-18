using System.Text.Json;
using ACI.Core.Abstractions;
using ACI.Core.Models;

namespace ACI.LLM.Services;

/// <summary>
/// 解析后的工具调用结果。
/// </summary>
public sealed class ResolvedActionCall
{
    /// <summary>
    /// 目标窗口 ID。
    /// </summary>
    public required string WindowId { get; init; }

    /// <summary>
    /// 命名空间 ID。
    /// </summary>
    public required string NamespaceId { get; init; }

    /// <summary>
    /// 工具 ID。
    /// </summary>
    public required string ActionId { get; init; }

    /// <summary>
    /// 完整工具 ID（namespace.action）。
    /// </summary>
    public string QualifiedActionId => $"{NamespaceId}.{ActionId}";

    /// <summary>
    /// 工具执行模式。
    /// </summary>
    public required ActionExecutionMode Mode { get; init; }

    /// <summary>
    /// 参数。
    /// </summary>
    public JsonElement? Parameters { get; init; }
}

/// <summary>
/// 工具调用解析结果。
/// </summary>
public sealed class ActionCallResolution
{
    /// <summary>
    /// 是否成功。
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 失败原因。
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// 成功时的解析结果。
    /// </summary>
    public ResolvedActionCall? Action { get; init; }

    /// <summary>
    /// 创建成功结果。
    /// </summary>
    public static ActionCallResolution Ok(ResolvedActionCall action)
        => new() { Success = true, Action = action };

    /// <summary>
    /// 创建失败结果。
    /// </summary>
    public static ActionCallResolution Fail(string error)
        => new() { Success = false, Error = error };
}

/// <summary>
/// 工具调用名称解析器。
/// </summary>
public static class ActionCallResolver
{
    /// <summary>
    /// 根据窗口可见命名空间解析工具调用。
    /// </summary>
    public static ActionCallResolution Resolve(
        ParsedAction action,
        Window? window,
        IActionNamespaceRegistry? actionNamespaces)
    {
        if (window == null)
        {
            return ActionCallResolution.Fail($"Window '{action.WindowId}' does not exist");
        }

        if (actionNamespaces == null)
        {
            return ActionCallResolution.Fail("Action namespace registry is unavailable");
        }

        var visibleNamespaces = window.NamespaceRefs
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (visibleNamespaces.Count == 0)
        {
            return ActionCallResolution.Fail($"Window '{window.Id}' has no visible action namespaces");
        }

        var split = SplitQualifiedActionId(action.ActionId);
        if (split != null)
        {
            return ResolveQualified(action, visibleNamespaces, actionNamespaces, split.Value.NamespaceId, split.Value.ActionId);
        }

        return ResolveShort(action, visibleNamespaces, actionNamespaces);
    }

    /// <summary>
    /// 解析 namespace.action 形式。
    /// </summary>
    private static ActionCallResolution ResolveQualified(
        ParsedAction action,
        IReadOnlyList<string> visibleNamespaces,
        IActionNamespaceRegistry registry,
        string namespaceId,
        string toolId)
    {
        if (!visibleNamespaces.Contains(namespaceId, StringComparer.OrdinalIgnoreCase))
        {
            return ActionCallResolution.Fail(
                $"Action namespace '{namespaceId}' is not visible for window '{action.WindowId}'");
        }

        if (!registry.TryGetAction(namespaceId, toolId, out var tool) || tool == null)
        {
            return ActionCallResolution.Fail($"Action '{namespaceId}.{toolId}' does not exist");
        }

        return ActionCallResolution.Ok(new ResolvedActionCall
        {
            WindowId = action.WindowId,
            NamespaceId = namespaceId,
            ActionId = toolId,
            Mode = tool.Mode,
            Parameters = action.Parameters
        });
    }

    /// <summary>
    /// 解析短工具名。
    /// </summary>
    private static ActionCallResolution ResolveShort(
        ParsedAction action,
        IReadOnlyList<string> visibleNamespaces,
        IActionNamespaceRegistry registry)
    {
        var matches = new List<(string NamespaceId, ActionDescriptor Tool)>();

        foreach (var ns in visibleNamespaces)
        {
            if (registry.TryGetAction(ns, action.ActionId, out var tool) && tool != null)
            {
                matches.Add((ns, tool));
            }
        }

        if (matches.Count == 0)
        {
            return ActionCallResolution.Fail($"Action '{action.ActionId}' is not visible for window '{action.WindowId}'");
        }

        if (matches.Count > 1)
        {
            var candidates = string.Join(
                ", ",
                matches.Select(m => $"{m.NamespaceId}.{m.Tool.Id}").OrderBy(v => v, StringComparer.OrdinalIgnoreCase));

            return ActionCallResolution.Fail(
                $"Ambiguous action id '{action.ActionId}'. Candidates: {candidates}");
        }

        var matched = matches[0];
        return ActionCallResolution.Ok(new ResolvedActionCall
        {
            WindowId = action.WindowId,
            NamespaceId = matched.NamespaceId,
            ActionId = matched.Tool.Id,
            Mode = matched.Tool.Mode,
            Parameters = action.Parameters
        });
    }

    /// <summary>
    /// 拆分完整工具名。
    /// </summary>
    private static (string NamespaceId, string ActionId)? SplitQualifiedActionId(string actionId)
    {
        var dotIndex = actionId.IndexOf('.');
        if (dotIndex <= 0 || dotIndex >= actionId.Length - 1)
        {
            return null;
        }

        var namespaceId = actionId[..dotIndex].Trim();
        var toolId = actionId[(dotIndex + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(namespaceId) || string.IsNullOrWhiteSpace(toolId))
        {
            return null;
        }

        return (namespaceId, toolId);
    }
}
