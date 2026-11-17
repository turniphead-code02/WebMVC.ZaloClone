using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZaloClone.Data;
using ZaloClone.Hubs;
using ZaloClone.Infrastructure;
using ZaloClone.Models.Entities;
using ZaloClone.Models.ViewModels.Chat;
using ZaloClone.Services.Storage;

namespace ZaloClone.Services.Chat;

public class ChatService : IChatService
{
    private readonly AppDbContext _context;
    private readonly IFileStorageService _storageService;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        AppDbContext context,
        IFileStorageService storageService,
        IHubContext<ChatHub> hubContext,
        ILogger<ChatService> logger)
    {
        _context = context;
        _storageService = storageService;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<ApiResponse<List<MessageDto>>> GetMessagesAsync(Guid conversationId, Guid userId, int skip = 0, int take = 50)
    {
        var isMember = await _context.ConversationMembers
            .AnyAsync(m => m.ConversationId == conversationId && m.UserId == userId && m.LeftAt == null);

        if (!isMember)
        {
            return ApiResponse<List<MessageDto>>.Fail("Bạn không phải thành viên của cuộc trò chuyện này.");
        }

        var messages = await _context.Messages
            .Where(m => m.ConversationId == conversationId)
            .Include(m => m.Sender)
            .Include(m => m.Attachments)
            .OrderBy(m => m.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(m => new MessageDto
            {
                Id = m.Id,
                SenderId = m.SenderId ?? Guid.Empty,
                SenderName = m.Sender != null ? m.Sender.DisplayName : "Hệ thống",
                SenderAvatar = m.Sender != null ? m.Sender.AvatarUrl : null,
                Content = m.IsRecalled ? "Tin nhắn đã được thu hồi" : (m.Content ?? ""),
                MessageType = m.MessageType,
                CreatedAt = m.CreatedAt,
                FormattedTime = m.CreatedAt.ToString("HH:mm"),
                IsSelf = m.SenderId == userId
            })
            .ToListAsync();

        return ApiResponse<List<MessageDto>>.Ok(messages);
    }

    public async Task<ApiResponse<MessageDto>> SendTextMessageAsync(Guid conversationId, Guid senderId, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return ApiResponse<MessageDto>.Fail("Nội dung tin nhắn không được để trống.");
        }

        var isMember = await _context.ConversationMembers
            .AnyAsync(m => m.ConversationId == conversationId && m.UserId == senderId && m.LeftAt == null);

        if (!isMember)
        {
            return ApiResponse<MessageDto>.Fail("Bạn không phải thành viên của cuộc trò chuyện này.");
        }

        var sender = await _context.Users.FindAsync(senderId);
        var now = DateTimeOffset.UtcNow;

        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderId = senderId,
            Content = content.Trim(),
            MessageType = "Text",
            CreatedAt = now
        };

        _context.Messages.Add(message);

        var conv = await _context.Conversations.FindAsync(conversationId);
        if (conv != null)
        {
            conv.LastMessageId = message.Id;
            conv.UpdatedAt = now;
        }

        await _context.SaveChangesAsync();

        var dto = new MessageDto
        {
            Id = message.Id,
            SenderId = senderId,
            SenderName = sender?.DisplayName ?? "Người dùng",
            SenderAvatar = sender?.AvatarUrl,
            Content = message.Content,
            MessageType = message.MessageType,
            CreatedAt = message.CreatedAt,
            FormattedTime = message.CreatedAt.ToString("HH:mm"),
            IsSelf = true
        };

        // Broadcast qua SignalR
        await _hubContext.Clients.Group(ChatHub.GetConversationGroupName(conversationId))
            .SendAsync("ReceiveMessage", new
            {
                id = message.Id,
                conversationId = conversationId,
                senderId = senderId,
                senderName = dto.SenderName,
                senderAvatar = dto.SenderAvatar ?? "",
                content = message.Content,
                messageType = message.MessageType,
                createdAt = dto.FormattedTime
            });

        return ApiResponse<MessageDto>.Ok(dto, "Gửi tin nhắn thành công.");
    }

    public async Task<ApiResponse<MessageDto>> SendFileAttachmentAsync(Guid conversationId, Guid senderId, IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return ApiResponse<MessageDto>.Fail("File không hợp lệ hoặc rỗng.");
        }

        var isMember = await _context.ConversationMembers
            .AnyAsync(m => m.ConversationId == conversationId && m.UserId == senderId && m.LeftAt == null);

        if (!isMember)
        {
            return ApiResponse<MessageDto>.Fail("Bạn không có quyền gửi file vào hội thoại này.");
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        bool isImage = ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp";
        string category = isImage ? "chat-images" : "chat-files";

        string storageKey;
        try
        {
            storageKey = await _storageService.SaveFileAsync(file, category);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lưu file {FileName}", file.FileName);
            return ApiResponse<MessageDto>.Fail($"Không thể lưu file: {ex.Message}");
        }

        var sender = await _context.Users.FindAsync(senderId);
        var now = DateTimeOffset.UtcNow;
        var fileUrl = _storageService.GetRelativeUrl(storageKey);

        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderId = senderId,
            Content = isImage ? $"[Ảnh] {file.FileName}" : $"[Tệp] {file.FileName}",
            MessageType = isImage ? "Image" : "File",
            CreatedAt = now
        };

        _context.Messages.Add(message);

        var attachment = new MessageAttachment
        {
            Id = Guid.NewGuid(),
            MessageId = message.Id,
            FileName = file.FileName,
            StorageKey = storageKey,
            FileType = file.ContentType,
            FileSize = file.Length,
            CreatedAt = now
        };

        _context.MessageAttachments.Add(attachment);

        var conv = await _context.Conversations.FindAsync(conversationId);
        if (conv != null)
        {
            conv.LastMessageId = message.Id;
            conv.UpdatedAt = now;
        }

        await _context.SaveChangesAsync();

        var dto = new MessageDto
        {
            Id = message.Id,
            SenderId = senderId,
            SenderName = sender?.DisplayName ?? "Người dùng",
            SenderAvatar = sender?.AvatarUrl,
            Content = message.Content,
            MessageType = message.MessageType,
            CreatedAt = message.CreatedAt,
            FormattedTime = message.CreatedAt.ToString("HH:mm"),
            IsSelf = true
        };

        // Broadcast SignalR bao gồm fileUrl và thông tin attachment
        await _hubContext.Clients.Group(ChatHub.GetConversationGroupName(conversationId))
            .SendAsync("ReceiveMessage", new
            {
                id = message.Id,
                conversationId = conversationId,
                senderId = senderId,
                senderName = dto.SenderName,
                senderAvatar = dto.SenderAvatar ?? "",
                content = message.Content,
                messageType = message.MessageType,
                fileUrl = fileUrl,
                fileName = file.FileName,
                fileSize = file.Length,
                createdAt = dto.FormattedTime
            });

        return ApiResponse<MessageDto>.Ok(dto, "Gửi file thành công.");
    }

    public async Task<ApiResponse> RecallMessageAsync(Guid messageId, Guid userId)
    {
        var message = await _context.Messages.FindAsync(messageId);
        if (message == null)
        {
            return ApiResponse.Fail("Không tìm thấy tin nhắn.");
        }

        if (message.SenderId != userId)
        {
            return ApiResponse.Fail("Bạn chỉ có thể thu hồi tin nhắn của chính mình.");
        }

        message.IsRecalled = true;
        message.RecalledAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();

        await _hubContext.Clients.Group(ChatHub.GetConversationGroupName(message.ConversationId))
            .SendAsync("MessageRecalled", new { messageId = message.Id, conversationId = message.ConversationId });

        return ApiResponse.Ok("Đã thu hồi tin nhắn.");
    }

    public async Task<ApiResponse<List<AttachmentDto>>> GetAttachmentsAsync(Guid conversationId, Guid userId)
    {
        var isMember = await _context.ConversationMembers
            .AnyAsync(m => m.ConversationId == conversationId && m.UserId == userId && m.LeftAt == null);

        if (!isMember)
        {
            return ApiResponse<List<AttachmentDto>>.Fail("Không có quyền truy cập.");
        }

        var attachments = await _context.MessageAttachments
            .Where(a => a.Message.ConversationId == conversationId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AttachmentDto
            {
                Id = a.Id,
                FileName = a.FileName,
                FileType = a.FileType,
                FileSize = a.FileSize,
                FormattedSize = a.FileSize >= 1024 * 1024
                    ? $"{a.FileSize / (1024 * 1024)}mb"
                    : $"{a.FileSize / 1024}kb",
                StorageKey = a.StorageKey,
                IconClass = a.FileType.Contains("pdf") ? "bi-file-earmark-pdf text-danger" :
                            a.FileType.Contains("image") ? "bi-image text-success" :
                            a.FileType.Contains("word") ? "bi-file-earmark-word text-primary" : "bi-file-earmark-zip text-warning",
                BadgeColor = a.FileType.Contains("pdf") ? "danger" :
                             a.FileType.Contains("image") ? "success" :
                             a.FileType.Contains("word") ? "primary" : "warning"
            })
            .ToListAsync();

        return ApiResponse<List<AttachmentDto>>.Ok(attachments);
    }
}
