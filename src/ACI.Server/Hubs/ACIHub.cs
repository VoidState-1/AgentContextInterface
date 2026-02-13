using ACI.Core.Models;
using ACI.Server.Services;
using Microsoft.AspNetCore.SignalR;

namespace ACI.Server.Hubs;

/// <summary>
/// ACI SignalR Hub - 实时通信
/// </summary>
public class ACIHub : Hub
{
    private readonly ISessionManager _sessionManager;

    public ACIHub(ISessionManager sessionManager)
    {
        _sessionManager = sessionManager;
    }

    /// <summary>
    /// 客户端加入会话
    /// </summary>
    public async Task JoinSession(string sessionId)
    {
        var session = _sessionManager.GetSession(sessionId);
        if (session == null)
        {
            await Clients.Caller.SendAsync("Error", $"会话不存在: {sessionId}");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
        await Clients.Caller.SendAsync("JoinedSession", sessionId);
    }

    /// <summary>
    /// 客户端离开会话
    /// </summary>
    public async Task LeaveSession(string sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionId);
        await Clients.Caller.SendAsync("LeftSession", sessionId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}

/// <summary>
/// Hub 通知器接口（推送事件携带 agentId）
/// </summary>
public interface IACIHubNotifier
{
    Task NotifyWindowCreated(string sessionId, string agentId, Window window);
    Task NotifyWindowUpdated(string sessionId, string agentId, Window window);
    Task NotifyWindowClosed(string sessionId, string agentId, string windowId);
}

/// <summary>
/// Hub 通知器实现
/// </summary>
public class ACIHubNotifier : IACIHubNotifier
{
    private readonly IHubContext<ACIHub> _hubContext;

    public ACIHubNotifier(IHubContext<ACIHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyWindowCreated(string sessionId, string agentId, Window window)
    {
        await _hubContext.Clients.Group(sessionId).SendAsync("WindowCreated", new
        {
            AgentId = agentId,
            window.Id,
            Description = window.Description?.Render(),
            Content = window.Render(),
            window.AppName,
            CreatedAt = window.Meta.CreatedAt,
            UpdatedAt = window.Meta.UpdatedAt
        });
    }

    public async Task NotifyWindowUpdated(string sessionId, string agentId, Window window)
    {
        await _hubContext.Clients.Group(sessionId).SendAsync("WindowUpdated", new
        {
            AgentId = agentId,
            window.Id,
            Description = window.Description?.Render(),
            Content = window.Render(),
            UpdatedAt = window.Meta.UpdatedAt
        });
    }

    public async Task NotifyWindowClosed(string sessionId, string agentId, string windowId)
    {
        await _hubContext.Clients.Group(sessionId).SendAsync("WindowClosed", new
        {
            AgentId = agentId,
            WindowId = windowId
        });
    }
}
