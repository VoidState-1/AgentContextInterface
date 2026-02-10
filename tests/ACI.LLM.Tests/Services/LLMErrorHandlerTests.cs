using ACI.LLM.Services;
using System.Net;

namespace ACI.LLM.Tests.Services;

public class LLMErrorHandlerTests
{
    // 测试点：429 和 5xx 的 HttpRequestException 应被判定为可重试。
    // 预期结果：ShouldRetry 返回 true。
    [Fact]
    public void ShouldRetry_WithHttp429Or5xx_ShouldReturnTrue()
    {
        var tooManyRequests = new HttpRequestException("429", null, HttpStatusCode.TooManyRequests);
        var serverError = new HttpRequestException("503", null, HttpStatusCode.ServiceUnavailable);

        Assert.True(LLMErrorHandler.ShouldRetry(tooManyRequests));
        Assert.True(LLMErrorHandler.ShouldRetry(serverError));
    }

    // 测试点：4xx（除 429）错误通常不应重试。
    // 预期结果：ShouldRetry 返回 false。
    [Fact]
    public void ShouldRetry_WithNormalClientError_ShouldReturnFalse()
    {
        var badRequest = new HttpRequestException("400", null, HttpStatusCode.BadRequest);

        Assert.False(LLMErrorHandler.ShouldRetry(badRequest));
    }

    // 测试点：TaskCanceledException（超时）应被判定为可重试。
    // 预期结果：ShouldRetry 返回 true。
    [Fact]
    public void ShouldRetry_WithTaskCanceledException_ShouldReturnTrue()
    {
        var timeout = new TaskCanceledException("timeout");

        Assert.True(LLMErrorHandler.ShouldRetry(timeout));
    }

    // 测试点：fallback 模型应按配置顺序向后切换。
    // 预期结果：返回当前模型后一个模型；最后一个模型返回 null。
    [Fact]
    public void GetFallbackModel_ShouldMoveToNextModel()
    {
        var config = new OpenRouterConfig
        {
            FallbackModels = ["m1", "m2", "m3"]
        };

        var fromM1 = LLMErrorHandler.GetFallbackModel(config, "m1");
        var fromM3 = LLMErrorHandler.GetFallbackModel(config, "m3");

        Assert.Equal("m2", fromM1);
        Assert.Null(fromM3);
    }
}
