using System.Text.Json;
using ACI.Core.Abstractions;
using ACI.Core.Models;
using ACI.Framework.Components;
using ACI.Framework.Runtime;

namespace ACI.Framework.BuiltIn;

/// <summary>
/// 内置的 Agent 间通信应用。
/// 完全基于 MessageChannel（通用频道原语）构建，底层不知道此应用的存在。
/// 当 Session 中只有一个 Agent 时不注册此应用。
/// </summary>
public class MailboxApp : ContextApp
{
    /// <summary>
    /// 通信频道名称。所有 MailboxApp 实例共享此频道。
    /// </summary>
    private const string MailChannel = "agent.mail";

    private readonly List<AgentMessage> _inbox = [];
    private IDisposable? _subscription;

    public override string Name => "mailbox";
    public override string? AppDescription => "Send and receive messages from other agents.";

    public override void OnCreate()
    {
        _subscription = Context.MessageChannel.Subscribe(MailChannel, OnMailReceived);
    }

    public override void OnDestroy()
    {
        _subscription?.Dispose();
        _subscription = null;
    }

    public override ContextWindow CreateWindow(string? intent)
    {
        const string windowId = "mailbox";
        RegisterWindow(windowId);

        return new ContextWindow
        {
            Id = windowId,
            Description = new Text($"""
                Your communication mailbox. You are agent "{Context.Profile.Id}" ({Context.Profile.Name}).
                Use "send" to message another agent. Messages are delivered instantly.
                """),
            Content = RenderInbox(),
            Options = new WindowOptions { Important = true },
            Actions =
            [
                new ContextAction
                {
                    Id = "send",
                    Label = "Send message to another agent",
                    Params = Param.Object(new()
                    {
                        ["to"] = Param.String(),
                        ["content"] = Param.String()
                    }),
                    Handler = HandleSend
                },
                new ContextAction
                {
                    Id = "mark_read",
                    Label = "Mark all messages as read",
                    Handler = HandleMarkRead
                }
            ]
        };
    }

    private void OnMailReceived(ChannelMessage msg)
    {
        MailEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<MailEnvelope>(msg.Data);
        }
        catch
        {
            return;
        }

        if (envelope == null) return;
        if (envelope.To != Context.Profile.Id && envelope.To != "*") return;
        if (msg.SourceAgentId == Context.Profile.Id) return;

        _inbox.Add(new AgentMessage
        {
            Id = Guid.NewGuid().ToString("N"),
            FromAgentId = msg.SourceAgentId,
            ToAgentId = Context.Profile.Id,
            Content = envelope.Content,
            Timestamp = msg.Timestamp
        });

        RequestRefreshAll();
    }

    private Task<ActionResult> HandleSend(ActionContext ctx)
    {
        var to = ctx.GetString("to");
        var content = ctx.GetString("content");

        if (string.IsNullOrWhiteSpace(to) || string.IsNullOrWhiteSpace(content))
        {
            return Task.FromResult(ActionResult.Fail("'to' and 'content' are required."));
        }

        Context.MessageChannel.Post(
            MailChannel,
            JsonSerializer.Serialize(new MailEnvelope(to, content)),
            MessageScope.Session);

        return Task.FromResult(ActionResult.Ok(
            message: $"Message sent to {to}",
            shouldRefresh: true));
    }

    private Task<ActionResult> HandleMarkRead(ActionContext ctx)
    {
        foreach (var m in _inbox)
        {
            m.IsRead = true;
        }

        return Task.FromResult(ActionResult.Ok(shouldRefresh: true));
    }

    private IComponent RenderInbox()
    {
        if (_inbox.Count == 0)
        {
            return new Text("(empty inbox)");
        }

        var lines = new List<IComponent>();
        var unread = _inbox.Where(m => !m.IsRead).ToList();
        var read = _inbox.Where(m => m.IsRead).ToList();

        if (unread.Count > 0)
        {
            lines.Add(new Text($"--- Unread ({unread.Count}) ---"));
            foreach (var msg in unread)
            {
                lines.Add(new Text($"* [{msg.Timestamp:HH:mm:ss}] From {msg.FromAgentId}: {msg.Content}"));
            }
        }

        if (read.Count > 0)
        {
            lines.Add(new Text($"--- Read ({read.Count}) ---"));
            foreach (var msg in read)
            {
                lines.Add(new Text($"  [{msg.Timestamp:HH:mm:ss}] From {msg.FromAgentId}: {msg.Content}"));
            }
        }

        return new VStack { Children = lines };
    }

    private sealed record MailEnvelope(string To, string Content);
}
