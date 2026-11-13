using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ZaloClone.Data;
using ZaloClone.Hubs;
using ZaloClone.Infrastructure;
using ZaloClone.Models;
using ZaloClone.Models.Entities;
using ZaloClone.Models.ViewModels.Chat;

namespace ZaloClone.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly AppDbContext _context;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<HomeController> _logger;

    public HomeController(AppDbContext context, IHubContext<ChatHub> hubContext, ILogger<HomeController> logger)
    {
        _context = context;
        _hubContext = hubContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid? conversationId)
    {
        var currentUserId = User.GetUserId();
        var currentUser = await _context.Users.FindAsync(currentUserId);
        if (currentUser == null)
        {
            return RedirectToAction("Login", "Account");
        }

        // Lấy tất cả hội thoại của user
        var memberships = await _context.ConversationMembers
            .Where(m => m.UserId == currentUserId && m.LeftAt == null)
            .Include(m => m.Conversation)
                .ThenInclude(c => c.DirectConversation)
                    .ThenInclude(d => d.User1)
            .Include(m => m.Conversation)
                .ThenInclude(c => c.DirectConversation)
                    .ThenInclude(d => d.User2)
            .Include(m => m.Conversation)
                .ThenInclude(c => c.LastMessage)
                    .ThenInclude(lm => lm!.Sender)
            .OrderByDescending(m => m.Conversation.UpdatedAt)
            .ToListAsync();

        var convSummaries = new List<ConversationSummaryDto>();

        foreach (var m in memberships)
        {
            var c = m.Conversation;
            string title;
            string? avatarUrl = c.AvatarUrl;
            string tagText;
            string tagColor;

            if (c.IsGroup)
            {
                title = c.Title ?? "Nhóm chat";
                tagText = "Nhóm đồ án";
                tagColor = "primary";
            }
            else
            {
                var otherUser = c.DirectConversation?.UserId1 == currentUserId
                    ? c.DirectConversation?.User2
                    : c.DirectConversation?.User1;
                title = otherUser?.DisplayName ?? "Trò chuyện";
                avatarUrl = otherUser?.AvatarUrl;
                tagText = otherUser?.Role == "Admin" ? "Quản trị" : "Bạn bè";
                tagColor = otherUser?.Role == "Admin" ? "danger" : "info";
            }

            var lastMsg = c.LastMessage;
            string preview = lastMsg != null
                ? (lastMsg.SenderId == currentUserId ? $"Bạn: {lastMsg.Content}" : lastMsg.Content)
                : "Chưa có tin nhắn";

            string formattedTime = "";
            if (lastMsg != null)
            {
                var diff = DateTimeOffset.UtcNow - lastMsg.CreatedAt;
                if (diff.TotalMinutes < 60)
                    formattedTime = $"{(int)diff.TotalMinutes}m";
                else if (diff.TotalHours < 24)
                    formattedTime = $"{(int)diff.TotalHours}h";
                else
                    formattedTime = $"{(int)diff.TotalDays}d";
            }

            convSummaries.Add(new ConversationSummaryDto
            {
                Id = c.Id,
                Title = title,
                AvatarUrl = avatarUrl,
                IsGroup = c.IsGroup,
                LastMessageContent = preview,
                LastMessageTime = lastMsg?.CreatedAt,
                FormattedTime = formattedTime,
                TagText = tagText,
                TagColor = tagColor,
                IsActive = false,
                IsOnline = true
            });
        }

        // Chọn conversation active
        var activeId = conversationId ?? convSummaries.FirstOrDefault()?.Id;
        ConversationDetailDto? activeConvDto = null;

        if (activeId.HasValue)
        {
            var activeSummary = convSummaries.FirstOrDefault(cs => cs.Id == activeId.Value);
            if (activeSummary != null)
            {
                activeSummary.IsActive = true;
            }

            var messages = await _context.Messages
                .Where(msg => msg.ConversationId == activeId.Value)
                .Include(msg => msg.Sender)
                .OrderBy(msg => msg.CreatedAt)
                .Take(50)
                .Select(msg => new MessageDto
                {
                    Id = msg.Id,
                    SenderId = msg.SenderId ?? Guid.Empty,
                    SenderName = msg.Sender != null ? msg.Sender.DisplayName : "Hệ thống",
                    SenderAvatar = msg.Sender != null ? msg.Sender.AvatarUrl : null,
                    Content = msg.Content ?? "",
                    MessageType = msg.MessageType,
                    CreatedAt = msg.CreatedAt,
                    FormattedTime = msg.CreatedAt.ToString("HH:mm"),
                    IsSelf = msg.SenderId == currentUserId
                })
                .ToListAsync();

            var members = await _context.ConversationMembers
                .Where(m => m.ConversationId == activeId.Value && m.LeftAt == null)
                .Include(m => m.User)
                .Select(m => new MemberDto
                {
                    UserId = m.UserId,
                    DisplayName = m.User.DisplayName,
                    AvatarUrl = m.User.AvatarUrl,
                    Role = m.Role,
                    Bio = m.User.Bio ?? (m.Role == "Admin" ? "Trưởng nhóm" : "Thành viên"),
                    IsOnline = true
                })
                .ToListAsync();

            var attachments = await _context.MessageAttachments
                .Where(a => a.Message.ConversationId == activeId.Value)
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

            var activeConvEntity = await _context.Conversations.FindAsync(activeId.Value);

            activeConvDto = new ConversationDetailDto
            {
                Id = activeId.Value,
                Title = activeSummary?.Title ?? "Hội thoại",
                AvatarUrl = activeSummary?.AvatarUrl,
                IsGroup = activeConvEntity?.IsGroup ?? false,
                IsOnline = true,
                Messages = messages,
                Members = members,
                Attachments = attachments
            };
        }

        var viewModel = new ChatViewModel
        {
            CurrentUser = currentUser,
            Conversations = convSummaries,
            ActiveConversation = activeConvDto
        };

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> GetConversationData(Guid conversationId)
    {
        var currentUserId = User.GetUserId();

        var messages = await _context.Messages
            .Where(msg => msg.ConversationId == conversationId)
            .Include(msg => msg.Sender)
            .OrderBy(msg => msg.CreatedAt)
            .Take(50)
            .Select(msg => new MessageDto
            {
                Id = msg.Id,
                SenderId = msg.SenderId ?? Guid.Empty,
                SenderName = msg.Sender != null ? msg.Sender.DisplayName : "Hệ thống",
                SenderAvatar = msg.Sender != null ? msg.Sender.AvatarUrl : null,
                Content = msg.Content ?? "",
                MessageType = msg.MessageType,
                CreatedAt = msg.CreatedAt,
                FormattedTime = msg.CreatedAt.ToString("HH:mm"),
                IsSelf = msg.SenderId == currentUserId
            })
            .ToListAsync();

        var members = await _context.ConversationMembers
            .Where(m => m.ConversationId == conversationId && m.LeftAt == null)
            .Include(m => m.User)
            .Select(m => new MemberDto
            {
                UserId = m.UserId,
                DisplayName = m.User.DisplayName,
                AvatarUrl = m.User.AvatarUrl,
                Role = m.Role,
                Bio = m.User.Bio ?? "",
                IsOnline = true
            })
            .ToListAsync();

        var attachments = await _context.MessageAttachments
            .Where(a => a.Message.ConversationId == conversationId)
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

        return Json(new { success = true, messages, members, attachments });
    }

    [HttpPost]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest req)
    {
        var currentUserId = User.GetUserId();
        if (currentUserId == Guid.Empty || string.IsNullOrWhiteSpace(req?.Content))
        {
            return BadRequest(new { success = false, message = "Nội dung không hợp lệ" });
        }

        var isMember = await _context.ConversationMembers
            .AnyAsync(m => m.ConversationId == req.ConversationId && m.UserId == currentUserId && m.LeftAt == null);
        if (!isMember)
        {
            return Forbid();
        }

        var sender = await _context.Users.FindAsync(currentUserId);
        var now = DateTimeOffset.UtcNow;
        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = req.ConversationId,
            SenderId = currentUserId,
            Content = req.Content.Trim(),
            MessageType = "Text",
            CreatedAt = now
        };

        _context.Messages.Add(message);

        var conv = await _context.Conversations.FindAsync(req.ConversationId);
        if (conv != null)
        {
            conv.LastMessageId = message.Id;
            conv.UpdatedAt = now;
        }

        await _context.SaveChangesAsync();

        var payload = new
        {
            id = message.Id,
            conversationId = message.ConversationId,
            senderId = message.SenderId,
            senderName = sender?.DisplayName ?? "Người dùng",
            senderAvatar = sender?.AvatarUrl ?? "",
            content = message.Content,
            createdAt = message.CreatedAt.ToString("HH:mm"),
            formattedDate = message.CreatedAt.ToString("dd/MM/yyyy HH:mm")
        };

        // Bắn SignalR broadcast
        await _hubContext.Clients.Group(ChatHub.GetConversationGroupName(req.ConversationId))
            .SendAsync("ReceiveMessage", payload);

        return Json(new { success = true, data = payload });
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

public class SendMessageRequest
{
    public Guid ConversationId { get; set; }
    public string Content { get; set; } = string.Empty;
}
