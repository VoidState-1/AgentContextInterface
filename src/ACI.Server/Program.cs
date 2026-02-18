using ACI.LLM;
using ACI.LLM.Abstractions;
using ACI.LLM.Services;
using ACI.Server.Endpoints;
using ACI.Server.Hubs;
using ACI.Server.Services;
using ACI.Server.Settings;
using ACI.Server.Dto;
using ACI.Storage;

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
builder.Services.AddSingleton<ISessionStore>(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ACIOptions>>().Value;
    var configuredPath = options.Persistence.SessionStorePath;
    var basePath = Path.IsPathRooted(configuredPath)
        ? configuredPath
        : Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, configuredPath));
    return new FileSessionStore(basePath);
});

builder.Services.AddSingleton<ISessionManager>(sp =>
{
    var llmBridge = sp.GetRequiredService<ILLMBridge>();
    var hubNotifier = sp.GetRequiredService<IACIHubNotifier>();
    var store = sp.GetRequiredService<ISessionStore>();
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ACIOptions>>().Value;
    return new SessionManager(llmBridge, hubNotifier, store, options);
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
app.MapPersistenceEndpoints();
app.MapInteractionEndpoints();
app.MapWindowEndpoints();

// SignalR Hub
app.MapHub<ACIHub>("/hubs/ACI");

// 健康检查
app.MapGet("/health", () => Results.Ok(new HealthResponse
{
    Status = "Healthy",
    Time = DateTime.UtcNow
}));

// ========== 启动 ==========

app.Run();
