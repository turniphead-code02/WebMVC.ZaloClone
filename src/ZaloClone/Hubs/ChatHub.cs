using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ZaloClone.Data;
using ZaloClone.Infrastructure;
using ZaloClone.Models.Entities;

namespace ZaloClone.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> _onlineUsers = new();

    private readonly AppDbContext _dbContext;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(AppDbContext dbContext, ILogger<ChatHub> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public static bool IsUserOnline(Guid userId) =>
        _onlineUsers.TryGetValue(userId, out var connections) && !connections.IsEmpty;

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User.GetUserId();
        if (userId != Guid.Empty)
        {
            var conns = _onlineUsers.GetOrAdd(userId, _ => new ConcurrentDictionary<string, byte>());
            bool isFirstConnection = conns.IsEmpty;
            conns.TryAdd(Context.ConnectionId, 0);

            // Thêm vào group riêng của user để nhận thông báo cá nhân & tin nhắn hội thoại mới
            await Groups.AddToGroupAsync(Context.ConnectionId, GetUserGroupName(userId));

            // Thêm vào tất cả các group hội thoại mà user đang tham gia
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
                await Clients.Others.SendAsync("PresenceChanged", new
                {
                    userId,
                    isOnline = true,
                    lastActiveAt = DateTimeOffset.UtcNow
                });
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
                await Clients.Others.SendAsync("PresenceChanged", new
                {
                    userId,
                    isOnline = false,
                    lastActiveAt = now
                });

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
    /// Gửi tin nhắn tức thời qua SignalR WebSocket
    /// </summary>
    public async Task SendMessage(Guid conversationId, string content, Guid? replyToId = null)
    {
        var userId = Context.User.GetUserId();
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(content)) return;

        var trimmed = content.Trim();
        if (trimmed.Length > 4000)
        {
            await Clients.Caller.SendAsync("ReceiveError", "Tin nhắn không được vượt quá 4.000 ký tự.");
            return;
        }

        var activeMemberIds = await _dbContext.ConversationMembers
            .Where(m => m.ConversationId == conversationId && m.LeftAt == null)
            .Select(m => m.UserId)
            .ToListAsync();

        if (!activeMemberIds.Contains(userId)) return;

        var conv = await _dbContext.Conversations
            .Include(c => c.DirectConversation)
            .FirstOrDefaultAsync(c => c.Id == conversationId);
        if (conv == null) return;

        // CHAT-18: Kiểm tra chặn trong hội thoại 1-1
        if (!conv.IsGroup && conv.DirectConversation != null)
        {
            var u1 = conv.DirectConversation.UserId1;
            var u2 = conv.DirectConversation.UserId2;
            var isBlocked = await _dbContext.UserBlocks.AnyAsync(b =>
                (b.BlockerId == u1 && b.BlockedId == u2) ||
                (b.BlockerId == u2 && b.BlockedId == u1));
            if (isBlocked)
            {
                await Clients.Caller.SendAsync("ReceiveError", "Không thể gửi tin nhắn do một trong hai người đã chặn nhau.");
                return;
            }
        }

        Message? replyMsg = null;
        if (replyToId.HasValue)
        {
            replyMsg = await _dbContext.Messages
                .Include(m => m.Sender)
                .FirstOrDefaultAsync(m => m.Id == replyToId.Value && m.ConversationId == conversationId);
        }

        var sender = await _dbContext.Users.FindAsync(userId);
        var now = DateTimeOffset.UtcNow;
        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderId = userId,
            Content = trimmed,
            MessageType = "Text",
            ReplyToId = replyMsg?.Id,
            CreatedAt = now
        };

        _dbContext.Messages.Add(message);
        conv.LastMessageId = message.Id;
        conv.UpdatedAt = now;

        await _dbContext.SaveChangesAsync();

        var replySenderName = replyMsg != null ? (replyMsg.Sender?.DisplayName ?? "Hệ thống") : null;
        var replyContent = replyMsg != null
            ? (replyMsg.IsRecalled ? "Tin nhắn đã được thu hồi" : (replyMsg.Content ?? ""))
            : null;

        // Gửi tới từng user group để hội thoại mới tạo cũng nhận được ngay không cần F5
        await Clients.Groups(activeMemberIds.Select(GetUserGroupName).ToList()).SendAsync("ReceiveMessage", new
        {
            id = message.Id,
            conversationId = message.ConversationId,
            senderId = message.SenderId,
            senderName = sender?.DisplayName ?? "Người dùng",
            senderAvatar = sender?.AvatarUrl ?? "",
            content = message.Content,
            messageType = message.MessageType,
            replyToId = message.ReplyToId,
            replyToSenderName = replySenderName,
            replyToContent = replyContent,
            isPinned = false,
            createdAt = message.CreatedAt.ToString("HH:mm"),
            formattedDate = message.CreatedAt.ToString("dd/MM/yyyy HH:mm")
        });
    }

    public static string GetUserGroupName(Guid userId) => $"user:{userId}";
    public static string GetConversationGroupName(Guid conversationId) => $"conv:{conversationId}";
}
