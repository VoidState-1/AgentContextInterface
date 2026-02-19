using ACI.Server.Dto;
using ACI.Server.Services;

namespace ACI.Server.Endpoints;

/// <summary>
/// AI 交互端点（Agent 级）。
/// </summary>
public static class InteractionEndpoints
{
    /// <summary>
    /// 注册交互 API。
    /// </summary>
    public static void MapInteractionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/sessions/{sessionId}/agents/{agentId}/interact")
            .WithTags("Interaction");

        // 处理用户消息主入口：触发 LLM 交互与自动 action 循环。
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
                return Results.NotFound(new ErrorResponse { Error = $"Session not found: {sessionId}" });
            }

            if (session.GetAgent(agentId) == null)
            {
                return Results.NotFound(new ErrorResponse { Error = $"Agent not found: {agentId}" });
            }

            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return Results.BadRequest(new ErrorResponse { Error = "Message cannot be empty." });
            }

            var result = await session.InteractAsync(agentId, request.Message, ct);

            return Results.Ok(result.Success
                ? ToSuccessResponse(result)
                : ToFailedResponse(result));
        });

        // 注入模拟 assistant 输出（调试专用，不调用模型）。
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
                return Results.NotFound(new ErrorResponse { Error = $"Session not found: {sessionId}" });
            }

            if (session.GetAgent(agentId) == null)
            {
                return Results.NotFound(new ErrorResponse { Error = $"Agent not found: {agentId}" });
            }

            if (string.IsNullOrWhiteSpace(request.AssistantOutput))
            {
                return Results.BadRequest(new ErrorResponse { Error = "AssistantOutput cannot be empty." });
            }

            var result = await session.SimulateAsync(agentId, request.AssistantOutput, ct);

            return Results.Ok(result.Success
                ? ToSuccessResponse(result)
                : ToFailedResponse(result));
        });
    }

    /// <summary>
    /// 交互失败时映射响应。
    /// </summary>
    private static InteractionResponse ToFailedResponse(ACI.LLM.InteractionResult result)
    {
        return new InteractionResponse
        {
            Success = false,
            Error = result.Error
        };
    }

    /// <summary>
    /// 交互成功时映射响应。
    /// </summary>
    private static InteractionResponse ToSuccessResponse(ACI.LLM.InteractionResult result)
    {
        return new InteractionResponse
        {
            Success = true,
            Response = result.Response,
            Action = result.Action != null
                ? new ActionInfo
                {
                    Type = "action",
                    AppName = null,
                    WindowId = result.Action.WindowId,
                    ActionId = result.Action.ActionId
                }
                : null,
            ActionResult = result.ActionResult != null
                ? new ActionResultInfo
                {
                    Success = result.ActionResult.Success,
                    Message = result.ActionResult.Message,
                    Summary = result.ActionResult.Summary
                }
                : null,
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
            Usage = result.Usage != null
                ? new TokenUsageInfo
                {
                    PromptTokens = result.Usage.PromptTokens,
                    CompletionTokens = result.Usage.CompletionTokens,
                    TotalTokens = result.Usage.TotalTokens
                }
                : null
        };
    }
}
