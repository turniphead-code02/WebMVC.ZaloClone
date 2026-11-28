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

    private static readonly string[] AllowedImageExts = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
    private static readonly string[] AllowedFileExts = { ".pdf", ".docx", ".xlsx", ".pptx", ".zip", ".txt" };

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
            .OrderByDescending(m => m.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(m => new MessageDto
            {
                Id = m.Id,
                SenderId = m.SenderId ?? Guid.Empty,
                SenderName = m.Sender != null ? m.Sender.DisplayName : "Hệ thống",
                SenderAvatar = m.Sender != null ? m.Sender.AvatarUrl : null,
                Content = m.IsRecalled ? "Tin nhắn đã được thu hồi" : (m.Content ?? ""),
                MessageType = m.IsRecalled ? "Recalled" : m.MessageType,
                CreatedAt = m.CreatedAt,
                FormattedTime = m.CreatedAt.ToString("HH:mm"),
                IsSelf = m.SenderId == userId
            })
            .ToListAsync();
        messages.Reverse();

        return ApiResponse<List<MessageDto>>.Ok(messages);
    }

    public async Task<ApiResponse<List<ChatMessageDetailDto>>> GetMessagesWithMetaAsync(Guid conversationId, Guid userId, int skip = 0, int take = 50)
    {
        var isMember = await _context.ConversationMembers
            .AnyAsync(m => m.ConversationId == conversationId && m.UserId == userId && m.LeftAt == null);

        if (!isMember)
        {
            return ApiResponse<List<ChatMessageDetailDto>>.Fail("Bạn không phải thành viên của cuộc trò chuyện này.");
        }

        var messages = await _context.Messages
            .Where(m => m.ConversationId == conversationId)
            .Include(m => m.Sender)
            .Include(m => m.Attachments)
            .Include(m => m.ReplyTo)
                .ThenInclude(r => r!.Sender)
            .OrderByDescending(m => m.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(m => new ChatMessageDetailDto
            {
                Id = m.Id,
                SenderId = m.SenderId ?? Guid.Empty,
                SenderName = m.Sender != null ? m.Sender.DisplayName : "Hệ thống",
                SenderAvatar = m.Sender != null ? m.Sender.AvatarUrl : null,
                Content = m.IsRecalled ? "Tin nhắn đã được thu hồi" : (m.Content ?? ""),
                MessageType = m.IsRecalled ? "Recalled" : m.MessageType,
                CreatedAt = m.CreatedAt,
                FormattedTime = m.CreatedAt.ToString("HH:mm"),
                IsSelf = m.SenderId == userId,
                ReplyToId = m.ReplyToId,
                ReplyToSenderName = m.ReplyTo != null
                    ? (m.ReplyTo.Sender != null ? m.ReplyTo.Sender.DisplayName : "Hệ thống")
                    : null,
                ReplyToContent = m.ReplyTo != null
                    ? (m.ReplyTo.IsRecalled ? "Tin nhắn đã được thu hồi" : (m.ReplyTo.Content ?? ""))
                    : null,
                IsPinned = m.IsPinned && !m.IsRecalled
            })
            .ToListAsync();
        messages.Reverse();

        return ApiResponse<List<ChatMessageDetailDto>>.Ok(messages);
    }

    private async Task<(bool Allowed, string? Error, List<Guid> MemberIds)> ValidateSendPermissionAsync(Guid conversationId, Guid senderId)
    {
        var activeMemberIds = await _context.ConversationMembers
            .Where(m => m.ConversationId == conversationId && m.LeftAt == null)
            .Select(m => m.UserId)
            .ToListAsync();

        if (!activeMemberIds.Contains(senderId))
        {
            return (false, "Bạn không phải thành viên của cuộc trò chuyện này.", activeMemberIds);
        }

        var conv = await _context.Conversations
            .Include(c => c.DirectConversation)
            .FirstOrDefaultAsync(c => c.Id == conversationId);
        if (conv != null && !conv.IsGroup && conv.DirectConversation != null)
        {
            var u1 = conv.DirectConversation.UserId1;
            var u2 = conv.DirectConversation.UserId2;
            var isBlocked = await _context.UserBlocks.AnyAsync(b =>
                (b.BlockerId == u1 && b.BlockedId == u2) ||
                (b.BlockerId == u2 && b.BlockedId == u1));
            if (isBlocked)
            {
                return (false, "Không thể gửi tin nhắn do một trong hai người đã chặn nhau.", activeMemberIds);
            }
        }

        return (true, null, activeMemberIds);
    }

    public async Task<ApiResponse<MessageDto>> SendTextMessageAsync(Guid conversationId, Guid senderId, string content, Guid? replyToId = null)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return ApiResponse<MessageDto>.Fail("Nội dung tin nhắn không được để trống.");
        }

        var trimmed = content.Trim();
        if (trimmed.Length > 4000)
        {
            return ApiResponse<MessageDto>.Fail("Tin nhắn không được vượt quá 4.000 ký tự.");
        }

        var perm = await ValidateSendPermissionAsync(conversationId, senderId);
        if (!perm.Allowed)
        {
            return ApiResponse<MessageDto>.Fail(perm.Error ?? "Không có quyền gửi tin nhắn.");
        }

        Message? replyMsg = null;
        if (replyToId.HasValue)
        {
            replyMsg = await _context.Messages
                .Include(m => m.Sender)
                .FirstOrDefaultAsync(m => m.Id == replyToId.Value && m.ConversationId == conversationId);
        }

        var sender = await _context.Users.FindAsync(senderId);
        var now = DateTimeOffset.UtcNow;

        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderId = senderId,
            Content = trimmed,
            MessageType = "Text",
            ReplyToId = replyMsg?.Id,
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

        var replySenderName = replyMsg != null ? (replyMsg.Sender?.DisplayName ?? "Hệ thống") : null;
        var replyContent = replyMsg != null
            ? (replyMsg.IsRecalled ? "Tin nhắn đã được thu hồi" : (replyMsg.Content ?? ""))
            : null;

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

        await _hubContext.Clients.Groups(perm.MemberIds.Select(ChatHub.GetUserGroupName).ToList())
            .SendAsync("ReceiveMessage", new
            {
                id = message.Id,
                conversationId = conversationId,
                senderId = senderId,
                senderName = dto.SenderName,
                senderAvatar = dto.SenderAvatar ?? "",
                content = message.Content,
                messageType = message.MessageType,
                replyToId = message.ReplyToId,
                replyToSenderName = replySenderName,
                replyToContent = replyContent,
                isPinned = false,
                createdAt = dto.FormattedTime
            });

        return ApiResponse<MessageDto>.Ok(dto, "Gửi tin nhắn thành công.");
    }

    public async Task<ApiResponse<MessageDto>> SendFileAttachmentAsync(Guid conversationId, Guid senderId, IFormFile file, Guid? replyToId = null)
    {
        if (file == null || file.Length == 0)
        {
            return ApiResponse<MessageDto>.Fail("File không hợp lệ hoặc rỗng.");
        }

        var perm = await ValidateSendPermissionAsync(conversationId, senderId);
        if (!perm.Allowed)
        {
            return ApiResponse<MessageDto>.Fail(perm.Error ?? "Bạn không có quyền gửi file vào hội thoại này.");
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        bool isImage = AllowedImageExts.Contains(ext);
        bool isDoc = AllowedFileExts.Contains(ext);

        if (!isImage && !isDoc)
        {
            return ApiResponse<MessageDto>.Fail("Định dạng không hỗ trợ. Chỉ chấp nhận ảnh (.jpg, .png, .webp) hoặc file (.pdf, .docx, .xlsx, .pptx, .zip, .txt).");
        }

        long maxBytes = isImage ? 10L * 1024 * 1024 : 50L * 1024 * 1024;
        if (file.Length > maxBytes)
        {
            return ApiResponse<MessageDto>.Fail(isImage
                ? "Dung lượng ảnh vượt quá giới hạn 10 MB."
                : "Dung lượng file đính kèm vượt quá giới hạn 50 MB.");
        }

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

        Message? replyMsg = null;
        if (replyToId.HasValue)
        {
            replyMsg = await _context.Messages
                .Include(m => m.Sender)
                .FirstOrDefaultAsync(m => m.Id == replyToId.Value && m.ConversationId == conversationId);
        }

        var sender = await _context.Users.FindAsync(senderId);
        var now = DateTimeOffset.UtcNow;
        var attachmentId = Guid.NewGuid();
        var fileUrl = $"/Chat/File/{attachmentId}";

        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderId = senderId,
            Content = isImage ? $"[Ảnh] {file.FileName}" : $"[Tệp] {file.FileName}",
            MessageType = isImage ? "Image" : "File",
            ReplyToId = replyMsg?.Id,
            CreatedAt = now
        };

        _context.Messages.Add(message);

        var attachment = new MessageAttachment
        {
            Id = attachmentId,
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

        var replySenderName = replyMsg != null ? (replyMsg.Sender?.DisplayName ?? "Hệ thống") : null;
        var replyContent = replyMsg != null
            ? (replyMsg.IsRecalled ? "Tin nhắn đã được thu hồi" : (replyMsg.Content ?? ""))
            : null;

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

        await _hubContext.Clients.Groups(perm.MemberIds.Select(ChatHub.GetUserGroupName).ToList())
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
                replyToId = message.ReplyToId,
                replyToSenderName = replySenderName,
                replyToContent = replyContent,
                isPinned = false,
                createdAt = dto.FormattedTime
            });

        return ApiResponse<MessageDto>.Ok(dto, "Gửi file thành công.");
    }

    public async Task<ApiResponse> RecallMessageAsync(Guid messageId, Guid userId)
    {
        var message = await _context.Messages
            .Include(m => m.Attachments)
            .FirstOrDefaultAsync(m => m.Id == messageId);
        if (message == null)
        {
            return ApiResponse.Fail("Không tìm thấy tin nhắn.");
        }

        if (message.SenderId != userId)
        {
            return ApiResponse.Fail("Bạn chỉ có thể thu hồi tin nhắn của chính mình.");
        }

        if (message.IsRecalled)
        {
            return ApiResponse.Fail("Tin nhắn này đã được thu hồi trước đó.");
        }

        // CHAT-14: Xóa file vật lý và attachment khi thu hồi tin nhắn có file
        if (message.Attachments.Count > 0)
        {
            foreach (var att in message.Attachments)
            {
                await _storageService.DeleteFileAsync(att.StorageKey);
            }
            _context.MessageAttachments.RemoveRange(message.Attachments);
        }

        message.IsRecalled = true;
        message.RecalledAt = DateTimeOffset.UtcNow;

        if (message.IsPinned)
        {
            message.IsPinned = false;
            message.PinnedAt = null;
            message.PinnedById = null;
        }

        await _context.SaveChangesAsync();

        await _hubContext.Clients.Group(ChatHub.GetConversationGroupName(message.ConversationId))
            .SendAsync("MessageRecalled", new { messageId = message.Id, conversationId = message.ConversationId });

        return ApiResponse.Ok("Đã thu hồi tin nhắn.");
    }

    public async Task<ApiResponse<ChatMessageDetailDto>> TogglePinMessageAsync(Guid messageId, Guid userId)
    {
        var message = await _context.Messages
            .Include(m => m.Sender)
            .FirstOrDefaultAsync(m => m.Id == messageId);

        if (message == null)
        {
            return ApiResponse<ChatMessageDetailDto>.Fail("Không tìm thấy tin nhắn.");
        }

        var isMember = await _context.ConversationMembers
            .AnyAsync(m => m.ConversationId == message.ConversationId && m.UserId == userId && m.LeftAt == null);

        if (!isMember)
        {
            return ApiResponse<ChatMessageDetailDto>.Fail("Bạn không phải thành viên của cuộc trò chuyện này.");
        }

        if (message.IsRecalled)
        {
            return ApiResponse<ChatMessageDetailDto>.Fail("Không thể ghim tin nhắn đã thu hồi.");
        }

        var now = DateTimeOffset.UtcNow;
        bool newPinState = !message.IsPinned;

        if (newPinState)
        {
            var oldPinned = await _context.Messages
                .Where(m => m.ConversationId == message.ConversationId && m.IsPinned && m.Id != message.Id)
                .ToListAsync();

            foreach (var old in oldPinned)
            {
                old.IsPinned = false;
                old.PinnedAt = null;
                old.PinnedById = null;
            }

            message.IsPinned = true;
            message.PinnedAt = now;
            message.PinnedById = userId;
        }
        else
        {
            message.IsPinned = false;
            message.PinnedAt = null;
            message.PinnedById = null;
        }

        await _context.SaveChangesAsync();

        var pinnedBy = await _context.Users.FindAsync(userId);
        var senderName = message.Sender?.DisplayName ?? "Hệ thống";
        var content = message.Content ?? "";

        await _hubContext.Clients.Group(ChatHub.GetConversationGroupName(message.ConversationId))
            .SendAsync("MessagePinned", new
            {
                conversationId = message.ConversationId,
                messageId = message.Id,
                isPinned = message.IsPinned,
                senderName = senderName,
                content = content,
                pinnedByName = pinnedBy?.DisplayName ?? "Người dùng"
            });

        var dto = new ChatMessageDetailDto
        {
            Id = message.Id,
            SenderId = message.SenderId ?? Guid.Empty,
            SenderName = senderName,
            SenderAvatar = message.Sender?.AvatarUrl,
            Content = content,
            MessageType = message.MessageType,
            CreatedAt = message.CreatedAt,
            FormattedTime = message.CreatedAt.ToString("HH:mm"),
            IsSelf = message.SenderId == userId,
            IsPinned = message.IsPinned
        };

        return ApiResponse<ChatMessageDetailDto>.Ok(dto, message.IsPinned ? "Đã ghim tin nhắn." : "Đã bỏ ghim tin nhắn.");
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
            .Where(a => a.Message.ConversationId == conversationId && !a.Message.IsRecalled)
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
