using ACI.Core.Services;
using ACI.LLM.Services;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ACI.LLM.Tests.Services;

public class OpenRouterClientTests
{
    // 测试点：成功响应应被正确解析为 LLMResponse，并透传 usage 字段。
    // 预期结果：Success=true，内容与 token 使用量匹配响应体。
    [Fact]
    public async Task SendAsync_WithValidResponse_ShouldParseSuccessfully()
    {
        var json = """
                   {
                     "model":"test-model",
                     "choices":[{"message":{"role":"assistant","content":"hello"}}],
                     "usage":{"prompt_tokens":10,"completion_tokens":5,"total_tokens":15}
                   }
                   """;
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        var result = await client.SendAsync([new LlmMessage { Role = "user", Content = "hi" }]);

        Assert.True(result.Success);
        Assert.Equal("hello", result.Content);
        Assert.Equal("test-model", result.Model);
        Assert.NotNull(result.Usage);
        Assert.Equal(10, result.Usage!.PromptTokens);
        Assert.Equal(15, result.Usage.TotalTokens);
    }

    // 测试点：API 返回 HTML 等非 JSON 内容时应返回可诊断错误。
    // 预期结果：Success=false 且错误包含 parse API response as JSON。
    [Fact]
    public async Task SendAsync_WithNonJsonBody_ShouldReturnParseError()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<!DOCTYPE html><html>error</html>", Encoding.UTF8, "text/html")
        });

        var result = await client.SendAsync([new LlmMessage { Role = "user", Content = "hi" }]);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("parse API response as JSON", result.Error);
    }

    // 测试点：非 2xx 响应应返回失败并包含状态码信息。
    // 预期结果：Success=false 且错误包含 API error 与状态码。
    [Fact]
    public async Task SendAsync_WithNonSuccessStatus_ShouldReturnFailure()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"error\":\"bad\"}", Encoding.UTF8, "application/json")
        }, maxRetries: 0);

        var result = await client.SendAsync([new LlmMessage { Role = "user", Content = "hi" }]);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("OpenRouter API error", result.Error);
        Assert.Contains("BadRequest", result.Error);
    }

    private static OpenRouterClient CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> handler,
        int maxRetries = 0)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(handler));
        var options = Options.Create(new OpenRouterConfig
        {
            BaseUrl = "https://openrouter.ai/api/v1",
            ApiKey = "test-key",
            DefaultModel = "test-model",
            MaxRetries = maxRetries
        });

        return new OpenRouterClient(httpClient, options);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
