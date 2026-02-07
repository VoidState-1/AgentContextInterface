using ACI.Core.Abstractions;
using ACI.Core.Models;
using ACI.Core.Services;
using ACI.Framework.Runtime;
using ACI.LLM;
using ACI.LLM.Abstractions;
using ACI.Server.Settings;
using System.Threading;

namespace ACI.Server.Services;

public class SessionContext : IDisposable
{
    public string SessionId { get; }
    public DateTime CreatedAt { get; }

    public ISeqClock Clock { get; }
    public IEventBus Events { get; }
    public IWindowManager Windows { get; }
    public IContextManager Context { get; }

    public RuntimeContext Runtime { get; }
    public FrameworkHost Host { get; }

    public InteractionController Interaction { get; }
    public ActionExecutor ActionExecutor { get; }

    private bool _disposed;
    private readonly SemaphoreSlim _sessionLock = new(1, 1);

    public SessionContext(
        string sessionId,
        ILLMBridge llmBridge,
        ACIOptions options,
        Action<FrameworkHost>? configureApps = null)
    {
        SessionId = sessionId;
        CreatedAt = DateTime.UtcNow;

        var maxTokens = Math.Max(1000, options.Render.MaxTokens);
        var minConversationTokens = Math.Clamp(options.Render.MinConversationTokens, 0, maxTokens);
        var maxContextItems = Math.Max(10, options.Context.MaxItems);
        var maxLogs = Math.Max(10, options.ActivityLog.MaxLogs);

        Clock = new SeqClock();
        Events = new EventBus();
        Windows = new WindowManager(Clock);
        Context = new ContextManager(Clock);
        Windows.OnChanged += OnWindowChanged;

        Runtime = new RuntimeContext(Windows, Events, Clock, Context);
        Host = new FrameworkHost(Runtime);
        ActionExecutor = new ActionExecutor(Windows, Clock, Events, Host.RefreshWindow);

        RegisterBuiltInApps(maxLogs);
        Host.Start("activity_log");

        configureApps?.Invoke(Host);
        Host.Launch("launcher");

        Interaction = new InteractionController(
            llmBridge,
            Host,
            Context,
            Windows,
            ActionExecutor,
            renderOptions: new RenderOptions
            {
                MaxTokens = maxTokens,
                MinConversationTokens = minConversationTokens
            },
            maxContextItems: maxContextItems
        );
    }

    private void RegisterBuiltInApps(int maxLogs)
    {
        Host.Register(new Framework.BuiltIn.AppLauncher(() => Host.GetApps().ToList()));
        Host.Register(new Framework.BuiltIn.ActivityLog(maxLogs));
        Host.Register(new Framework.BuiltIn.FileExplorerApp());
    }

    public async Task<T> RunSerializedAsync<T>(Func<Task<T>> action, CancellationToken ct = default)
    {
        await _sessionLock.WaitAsync(ct);
        try
        {
            return await action();
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    private void OnWindowChanged(WindowChangedEvent evt)
    {
        if (evt.Type == WindowEventType.Created)
        {
            Context.Add(new ContextItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = ContextItemType.Window,
                Content = evt.WindowId
            });
            return;
        }

        if (evt.Type == WindowEventType.Removed)
        {
            Context.MarkWindowObsolete(evt.WindowId);
            return;
        }

        if (evt.Type == WindowEventType.Updated &&
            evt.Window?.Options.RefreshMode == RefreshMode.Append)
        {
            Context.Add(new ContextItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = ContextItemType.Window,
                Content = evt.WindowId
            });
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Windows.OnChanged -= OnWindowChanged;
        _sessionLock.Dispose();
        GC.SuppressFinalize(this);
    }
}