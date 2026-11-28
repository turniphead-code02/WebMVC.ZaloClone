using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ZaloClone.Data;
using ZaloClone.Infrastructure;
using ZaloClone.Services.Chat;

namespace ZaloClone.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> _onlineUsers = new();

    private readonly AppDbContext _dbContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(AppDbContext dbContext, IServiceProvider serviceProvider, ILogger<ChatHub> logger)
    {
        _dbContext = dbContext;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public static bool IsUserOnline(Guid userId) =>
        _onlineUsers.TryGetValue(userId, out var connections) && !connections.IsEmpty;

    private async Task<List<string>> GetPeerUserGroupsAsync(Guid userId)
    {
        var convMemberIds = await _dbContext.ConversationMembers
            .Where(m => m.LeftAt == null && m.UserId != userId &&
                        _dbContext.ConversationMembers.Any(my => my.ConversationId == m.ConversationId && my.UserId == userId && my.LeftAt == null))
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync();

        var friendIds = await _dbContext.Friendships
            .Where(f => f.UserId1 == userId || f.UserId2 == userId)
            .Select(f => f.UserId1 == userId ? f.UserId2 : f.UserId1)
            .ToListAsync();

        return convMemberIds
            .Union(friendIds)
            .Distinct()
            .Select(GetUserGroupName)
            .ToList();
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User.GetUserId();
        if (userId != Guid.Empty)
        {
            var conns = _onlineUsers.GetOrAdd(userId, _ => new ConcurrentDictionary<string, byte>());
            bool isFirstConnection = conns.IsEmpty;
            conns.TryAdd(Context.ConnectionId, 0);

            await Groups.AddToGroupAsync(Context.ConnectionId, GetUserGroupName(userId));

            var activeConvIds = await _dbContext.ConversationMembers
                .Where(m => m.UserId == userId && m.LeftAt == null)
                .Select(m => m.ConversationId)
                .ToListAsync();

            foreach (var convId in activeConvIds)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, GetConversationGroupName(convId));
            }

            if (isFirstConnection)
            {
                var peerGroups = await GetPeerUserGroupsAsync(userId);
                if (peerGroups.Count > 0)
                {
                    await Clients.Groups(peerGroups).SendAsync("PresenceChanged", new
                    {
                        userId,
                        isOnline = true,
                        lastActiveAt = DateTimeOffset.UtcNow
                    });
                }
            }

            _logger.LogInformation("User {UserId} connected with ConnectionId {ConnectionId}", userId, Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User.GetUserId();
        if (userId != Guid.Empty)
        {
            bool wentOffline = false;
            if (_onlineUsers.TryGetValue(userId, out var conns))
            {
                conns.TryRemove(Context.ConnectionId, out _);
                if (conns.IsEmpty)
                {
                    _onlineUsers.TryRemove(userId, out _);
                    wentOffline = true;
                }
            }

            if (wentOffline)
            {
                var now = DateTimeOffset.UtcNow;
                var peerGroups = await GetPeerUserGroupsAsync(userId);
                if (peerGroups.Count > 0)
                {
                    await Clients.Groups(peerGroups).SendAsync("PresenceChanged", new
                    {
                        userId,
                        isOnline = false,
                        lastActiveAt = now
                    });
                }

                await _dbContext.Users
                    .Where(u => u.Id == userId)
                    .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastActiveAt, now));
            }

            _logger.LogInformation("User {UserId} disconnected", userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Báo trạng thái đang soạn tin
    /// </summary>
    public async Task Typing(Guid conversationId)
    {
        var userId = Context.User.GetUserId();
        if (userId == Guid.Empty) return;

        var senderName = Context.User.GetDisplayName();
        var memberIds = await _dbContext.ConversationMembers
            .Where(m => m.ConversationId == conversationId && m.LeftAt == null && m.UserId != userId)
            .Select(m => m.UserId)
            .ToListAsync();

        if (memberIds.Count > 0)
        {
            await Clients.Groups(memberIds.Select(GetUserGroupName).ToList())
                .SendAsync("UserTyping", new { conversationId, userId, senderName });
        }
    }

    /// <summary>
    /// Đánh dấu đã đọc tin nhắn mới nhất trong hội thoại
    /// </summary>
    public async Task MarkAsRead(Guid conversationId, Guid? lastMessageId)
    {
        var userId = Context.User.GetUserId();
        if (userId == Guid.Empty) return;

        var member = await _dbContext.ConversationMembers
            .FirstOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == userId && m.LeftAt == null);
        if (member == null) return;

        if (!lastMessageId.HasValue)
        {
            lastMessageId = await _dbContext.Messages
                .Where(m => m.ConversationId == conversationId)
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => (Guid?)m.Id)
                .FirstOrDefaultAsync();
        }

        if (lastMessageId.HasValue)
        {
            member.LastReadMessageId = lastMessageId;
        }
        member.LastReadAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Gửi tin nhắn tức thời qua SignalR WebSocket (ủy quyền tập trung về ChatService)
    /// </summary>
    public async Task SendMessage(Guid conversationId, string content, Guid? replyToId = null)
    {
        var userId = Context.User.GetUserId();
        if (userId == Guid.Empty) return;

        var chatService = _serviceProvider.GetRequiredService<IChatService>();
        var result = await chatService.SendTextMessageAsync(conversationId, userId, content, replyToId);
        if (!result.Success)
        {
            await Clients.Caller.SendAsync("ReceiveError", result.Message ?? "Không thể gửi tin nhắn.");
        }
    }

    // ==================== WEBRTC VOICE CALL SIGNALING ====================

    public async Task CallUser(Guid conversationId)
    {
        var callerId = Context.User.GetUserId();
        if (callerId == Guid.Empty) return;

        var peerIds = await _dbContext.ConversationMembers
            .Where(m => m.ConversationId == conversationId && m.LeftAt == null && m.UserId != callerId)
            .Select(m => m.UserId)
            .ToListAsync();

        if (peerIds.Count == 0) return;

        await Clients.Groups(peerIds.Select(GetUserGroupName).ToList()).SendAsync("IncomingCall", new
        {
            conversationId,
            callerId,
            callerName = Context.User.GetDisplayName()
        });
    }

    public async Task AcceptCall(Guid conversationId, Guid callerId)
    {
        var accepterId = Context.User.GetUserId();
        if (accepterId == Guid.Empty) return;

        await Clients.Group(GetUserGroupName(callerId)).SendAsync("CallAccepted", new
        {
            conversationId,
            accepterId,
            accepterName = Context.User.GetDisplayName()
        });
    }

    public async Task RejectCall(Guid conversationId, Guid callerId, string? reason = null)
    {
        var rejecterId = Context.User.GetUserId();
        if (rejecterId == Guid.Empty) return;

        await Clients.Group(GetUserGroupName(callerId)).SendAsync("CallRejected", new
        {
            conversationId,
            rejecterId,
            reason = reason ?? "Người nhận đã từ chối cuộc gọi."
        });
    }

    public async Task EndCall(Guid conversationId)
    {
        var userId = Context.User.GetUserId();
        if (userId == Guid.Empty) return;

        var peerIds = await _dbContext.ConversationMembers
            .Where(m => m.ConversationId == conversationId && m.LeftAt == null && m.UserId != userId)
            .Select(m => m.UserId)
            .ToListAsync();

        if (peerIds.Count > 0)
        {
            await Clients.Groups(peerIds.Select(GetUserGroupName).ToList()).SendAsync("CallEnded", new
            {
                conversationId,
                endedBy = userId
            });
        }
    }

    public async Task SendWebRtcSignal(Guid conversationId, string signalJson)
    {
        var userId = Context.User.GetUserId();
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(signalJson)) return;

        var peerIds = await _dbContext.ConversationMembers
            .Where(m => m.ConversationId == conversationId && m.LeftAt == null && m.UserId != userId)
            .Select(m => m.UserId)
            .ToListAsync();

        if (peerIds.Count > 0)
        {
            await Clients.Groups(peerIds.Select(GetUserGroupName).ToList()).SendAsync("ReceiveWebRtcSignal", new
            {
                conversationId,
                senderId = userId,
                signalJson
            });
        }
    }

    public static string GetUserGroupName(Guid userId) => $"user:{userId}";
    public static string GetConversationGroupName(Guid conversationId) => $"conv:{conversationId}";
}
