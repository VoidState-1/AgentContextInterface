using ACI.Server.Dto;
using ACI.Server.Services;

namespace ACI.Server.Endpoints;

/// <summary>
/// AI 交互端点（Agent 级）
/// </summary>
public static class InteractionEndpoints
{
    public static void MapInteractionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/sessions/{sessionId}/agents/{agentId}/interact")
            .WithTags("Interaction");

        // 发送消息（通过 Session 入口，支持唤起队列）
        group.MapPost("/", async (
            string sessionId,
            string agentId,
            MessageRequest request,
            ISessionManager sessionManager,
            CancellationToken ct) =>
        {
            var session = sessionManager.GetSession(sessionId);
            if (session == null)
            {
                return Results.NotFound(new { Error = $"会话不存在: {sessionId}" });
            }

            if (session.GetAgent(agentId) == null)
            {
                return Results.NotFound(new { Error = $"Agent 不存在: {agentId}" });
            }

            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return Results.BadRequest(new { Error = "消息不能为空" });
            }

            var result = await session.InteractAsync(agentId, request.Message, ct);

            return Results.Ok(result.Success
                ? ToSuccessResponse(result)
                : ToFailedResponse(result));
        });

        // 手动模拟 AI 输出（调试用）
        group.MapPost("/simulate", async (
            string sessionId,
            string agentId,
            SimulateRequest request,
            ISessionManager sessionManager,
            CancellationToken ct) =>
        {
            var session = sessionManager.GetSession(sessionId);
            if (session == null)
            {
                return Results.NotFound(new { Error = $"会话不存在: {sessionId}" });
            }

            if (session.GetAgent(agentId) == null)
            {
                return Results.NotFound(new { Error = $"Agent 不存在: {agentId}" });
            }

            if (string.IsNullOrWhiteSpace(request.AssistantOutput))
            {
                return Results.BadRequest(new { Error = "AssistantOutput 不能为空" });
            }

            var result = await session.SimulateAsync(agentId, request.AssistantOutput, ct);

            return Results.Ok(result.Success
                ? ToSuccessResponse(result)
                : ToFailedResponse(result));
        });
    }

    private static InteractionResponse ToFailedResponse(ACI.LLM.InteractionResult result)
    {
        return new InteractionResponse
        {
            Success = false,
            Error = result.Error
        };
    }

    private static InteractionResponse ToSuccessResponse(ACI.LLM.InteractionResult result)
    {
        return new InteractionResponse
        {
            Success = true,
            Response = result.Response,
            Action = result.Action != null ? new ActionInfo
            {
                Type = "action",
                AppName = null,
                WindowId = result.Action.WindowId,
                ActionId = result.Action.ActionId
            } : null,
            ActionResult = result.ActionResult != null ? new ActionResultInfo
            {
                Success = result.ActionResult.Success,
                Message = result.ActionResult.Message,
                Summary = result.ActionResult.Summary
            } : null,
            Steps = result.Steps?.Select(step => new InteractionStepInfo
            {
                CallId = step.CallId,
                WindowId = step.WindowId,
                ActionId = step.ActionId,
                ResolvedMode = step.ResolvedMode,
                Success = step.Success,
                Message = step.Message,
                Summary = step.Summary,
                TaskId = step.TaskId,
                Turn = step.Turn,
                Index = step.Index
            }).ToList(),
            Usage = result.Usage != null ? new TokenUsageInfo
            {
                PromptTokens = result.Usage.PromptTokens,
                CompletionTokens = result.Usage.CompletionTokens,
                TotalTokens = result.Usage.TotalTokens
            } : null
        };
    }
}
