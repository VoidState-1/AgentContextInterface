using ACI.LLM;
using ACI.LLM.Abstractions;
using ACI.LLM.Services;
using ACI.Server.Endpoints;
using ACI.Server.Hubs;
using ACI.Server.Services;
using ACI.Server.Settings;

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

// ACI 配置
builder.Services.Configure<ACIOptions>(
    builder.Configuration.GetSection(ACIOptions.SectionName));

// HTTP Client
builder.Services.AddHttpClient<ILLMBridge, OpenRouterClient>((sp, client) =>
{
    var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpenRouterConfig>>().Value;

    if (!string.IsNullOrWhiteSpace(config.BaseUrl))
    {
        var baseUrl = config.BaseUrl.TrimEnd('/') + "/";
        client.BaseAddress = new Uri(baseUrl);
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
    var hubNotifier = sp.GetRequiredService<IACIHubNotifier>();
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ACIOptions>>().Value;
    return new SessionManager(llmBridge, hubNotifier, options);
});

// Hub 通知器
builder.Services.AddSingleton<IACIHubNotifier, ACIHubNotifier>();

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
app.MapHub<ACIHub>("/hubs/ACI");

// 健康检查
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Time = DateTime.UtcNow }));

// ========== 启动 ==========

app.Run();
