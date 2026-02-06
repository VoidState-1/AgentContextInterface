using ContextUI.LLM;
using ContextUI.LLM.Abstractions;
using ContextUI.LLM.Services;
using ContextUI.Server.Endpoints;
using ContextUI.Server.Hubs;
using ContextUI.Server.Services;
using ContextUI.Server.Settings;

var builder = WebApplication.CreateBuilder(args);

// ========== 配置服务 ==========

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// SignalR
builder.Services.AddSignalR();

// OpenRouter 配置
builder.Services.Configure<OpenRouterConfig>(
    builder.Configuration.GetSection(OpenRouterConfig.SectionName));

// ContextUI 配置
builder.Services.Configure<ContextUIOptions>(
    builder.Configuration.GetSection(ContextUIOptions.SectionName));

// HTTP Client
builder.Services.AddHttpClient<ILLMBridge, OpenRouterClient>((sp, client) =>
{
    var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpenRouterConfig>>().Value;

    if (!string.IsNullOrWhiteSpace(config.BaseUrl))
    {
        client.BaseAddress = new Uri(config.BaseUrl);
    }

    if (config.TimeoutSeconds > 0)
    {
        client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
    }
});

// 会话管理器（单例）
builder.Services.AddSingleton<ISessionManager>(sp =>
{
    var llmBridge = sp.GetRequiredService<ILLMBridge>();
    var hubNotifier = sp.GetRequiredService<IContextUIHubNotifier>();
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ContextUIOptions>>().Value;
    return new SessionManager(llmBridge, hubNotifier, options);
});

// Hub 通知器
builder.Services.AddSingleton<IContextUIHubNotifier, ContextUIHubNotifier>();

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ========== 配置中间件 ==========

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

// ========== 映射端点 ==========

app.MapSessionEndpoints();
app.MapInteractionEndpoints();
app.MapWindowEndpoints();

// SignalR Hub
app.MapHub<ContextUIHub>("/hubs/contextui");

// 健康检查
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Time = DateTime.UtcNow }));

// ========== 启动 ==========

app.Run();
