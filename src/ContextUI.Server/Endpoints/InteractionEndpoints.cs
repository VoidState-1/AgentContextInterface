using ContextUI.Server.Dto;
using ContextUI.Server.Services;

namespace ContextUI.Server.Endpoints;

/// <summary>
/// AI 交互端点
/// </summary>
public static class InteractionEndpoints
{
    public static void MapInteractionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/sessions/{sessionId}/interact")
            .WithTags("Interaction");

        // 发送消息
        group.MapPost("/", async (
            string sessionId,
            MessageRequest request,
            ISessionManager sessionManager,
            CancellationToken ct) =>
        {
            // 验证会话
            var session = sessionManager.GetSession(sessionId);
            if (session == null)
            {
                return Results.NotFound(new { Error = $"会话不存在: {sessionId}" });
            }

            // 验证请求
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return Results.BadRequest(new { Error = "消息不能为空" });
            }

            // 处理请求
            var result = await session.RunSerializedAsync(
                () => session.Interaction.ProcessAsync(request.Message, ct),
                ct);

            if (!result.Success)
            {
                return Results.Ok(ToFailedResponse(result));
            }

            return Results.Ok(ToSuccessResponse(result));
        });

        // 手动模拟 AI 输出（调试用）
        group.MapPost("/simulate", async (
            string sessionId,
            SimulateRequest request,
            ISessionManager sessionManager,
            CancellationToken ct) =>
        {
            // 验证会话
            var session = sessionManager.GetSession(sessionId);
            if (session == null)
            {
                return Results.NotFound(new { Error = $"会话不存在: {sessionId}" });
            }

            // 验证请求
            if (string.IsNullOrWhiteSpace(request.AssistantOutput))
            {
                return Results.BadRequest(new { Error = "AssistantOutput 不能为空" });
            }

            var result = await session.RunSerializedAsync(
                () => session.Interaction.ProcessAssistantOutputAsync(request.AssistantOutput, ct),
                ct);

            if (!result.Success)
            {
                return Results.Ok(ToFailedResponse(result));
            }

            return Results.Ok(ToSuccessResponse(result));
        });
    }

    private static InteractionResponse ToFailedResponse(ContextUI.LLM.InteractionResult result)
    {
        return new InteractionResponse
        {
            Success = false,
            Error = result.Error
        };
    }

    private static InteractionResponse ToSuccessResponse(ContextUI.LLM.InteractionResult result)
    {
        return new InteractionResponse
        {
            Success = true,
            Response = result.Response,
            Action = result.Action != null ? new ActionInfo
            {
                Type = result.Action.Type,
                AppName = result.Action.AppName,
                WindowId = result.Action.WindowId,
                ActionId = result.Action.ActionId
            } : null,
            ActionResult = result.ActionResult != null ? new ActionResultInfo
            {
                Success = result.ActionResult.Success,
                Message = result.ActionResult.Message,
                Summary = result.ActionResult.Summary
            } : null,
            Usage = result.Usage != null ? new TokenUsageInfo
            {
                PromptTokens = result.Usage.PromptTokens,
                CompletionTokens = result.Usage.CompletionTokens,
                TotalTokens = result.Usage.TotalTokens
            } : null
        };
    }
}
