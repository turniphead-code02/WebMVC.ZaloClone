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
    private readonly AppDbContext _dbContext;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(AppDbContext dbContext, ILogger<ChatHub> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User.GetUserId();
        if (userId != Guid.Empty)
        {
            // Thêm vào group riêng của user để nhận thông báo cá nhân
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

            // Cập nhật trạng thái online
            await Clients.Others.SendAsync("PresenceChanged", new
            {
                userId,
                isOnline = true,
                lastActiveAt = DateTimeOffset.UtcNow
            });

            _logger.LogInformation("User {UserId} connected with ConnectionId {ConnectionId}", userId, Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User.GetUserId();
        if (userId != Guid.Empty)
        {
            var now = DateTimeOffset.UtcNow;
            await Clients.Others.SendAsync("PresenceChanged", new
            {
                userId,
                isOnline = false,
                lastActiveAt = now
            });

            // Cập nhật last_active_at trong DB
            await _dbContext.Users
                .Where(u => u.Id == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastActiveAt, now));

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

        await Clients.OthersInGroup(GetConversationGroupName(conversationId))
            .SendAsync("UserTyping", new { conversationId, userId });
    }

    /// <summary>
    /// Gửi tin nhắn tức thời qua SignalR WebSocket
    /// </summary>
    public async Task SendMessage(Guid conversationId, string content)
    {
        var userId = Context.User.GetUserId();
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(content)) return;

        var isMember = await _dbContext.ConversationMembers
            .AnyAsync(m => m.ConversationId == conversationId && m.UserId == userId && m.LeftAt == null);
        if (!isMember) return;

        var sender = await _dbContext.Users.FindAsync(userId);
        var now = DateTimeOffset.UtcNow;
        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderId = userId,
            Content = content.Trim(),
            MessageType = "Text",
            CreatedAt = now
        };

        _dbContext.Messages.Add(message);

        var conv = await _dbContext.Conversations.FindAsync(conversationId);
        if (conv != null)
        {
            conv.LastMessageId = message.Id;
            conv.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync();

        await Clients.Group(GetConversationGroupName(conversationId)).SendAsync("ReceiveMessage", new
        {
            id = message.Id,
            conversationId = message.ConversationId,
            senderId = message.SenderId,
            senderName = sender?.DisplayName ?? "Người dùng",
            senderAvatar = sender?.AvatarUrl ?? "",
            content = message.Content,
            createdAt = message.CreatedAt.ToString("HH:mm"),
            formattedDate = message.CreatedAt.ToString("dd/MM/yyyy HH:mm")
        });
    }

    public static string GetUserGroupName(Guid userId) => $"user:{userId}";
    public static string GetConversationGroupName(Guid conversationId) => $"conv:{conversationId}";
}

