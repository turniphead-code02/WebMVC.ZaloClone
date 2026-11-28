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
using ZaloClone.Services.Chat;

namespace ZaloClone.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly AppDbContext _context;
    private readonly IChatService _chatService;
    private readonly ILogger<HomeController> _logger;

    public HomeController(AppDbContext context, IChatService chatService, ILogger<HomeController> logger)
    {
        _context = context;
        _chatService = chatService;
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
            .Include(m => m.LastReadMessage)
            .Include(m => m.Conversation)
                .ThenInclude(c => c.DirectConversation)
                    .ThenInclude(d => d!.User1)
            .Include(m => m.Conversation)
                .ThenInclude(c => c.DirectConversation)
                    .ThenInclude(d => d!.User2)
            .Include(m => m.Conversation)
                .ThenInclude(c => c.LastMessage)
                    .ThenInclude(lm => lm!.Sender)
            .OrderByDescending(m => m.Conversation.UpdatedAt)
            .ToListAsync();

        var convIds = memberships.Select(m => m.ConversationId).ToList();
        var allMemberUserIdsByConv = await _context.ConversationMembers
            .Where(m => convIds.Contains(m.ConversationId) && m.LeftAt == null && m.UserId != currentUserId)
            .Select(m => new { m.ConversationId, m.UserId })
            .ToListAsync();

        // Batch query đếm tin nhắn chưa đọc (tránh N+1 query trong vòng lặp)
        var unreadCheckThresholds = memberships
            .Where(m => m.Conversation.LastMessage != null && m.LastReadMessageId != m.Conversation.LastMessageId)
            .ToDictionary(m => m.ConversationId, m => m.LastReadMessage?.CreatedAt ?? DateTimeOffset.MinValue);

        var unreadCounts = new Dictionary<Guid, int>();
        if (unreadCheckThresholds.Count > 0)
        {
            var checkConvIds = unreadCheckThresholds.Keys.ToList();
            var minThreshold = unreadCheckThresholds.Values.Min();
            var recentUnreadCandidates = await _context.Messages
                .AsNoTracking()
                .Where(msg => checkConvIds.Contains(msg.ConversationId) && msg.SenderId != currentUserId && msg.CreatedAt > minThreshold)
                .Select(msg => new { msg.ConversationId, msg.CreatedAt })
                .ToListAsync();

            foreach (var group in recentUnreadCandidates.GroupBy(x => x.ConversationId))
            {
                var threshold = unreadCheckThresholds[group.Key];
                unreadCounts[group.Key] = group.Count(x => x.CreatedAt > threshold);
            }
        }

        var convSummaries = new List<ConversationSummaryDto>();

        foreach (var m in memberships)
        {
            var c = m.Conversation;
            string title;
            string? avatarUrl = c.AvatarUrl;
            string tagText;
            string tagColor;
            Guid? otherUserId = null;
            bool isOnline;

            if (c.IsGroup)
            {
                title = c.Title ?? "Nhóm chat";
                tagText = "Nhóm đồ án";
                tagColor = "primary";
                isOnline = allMemberUserIdsByConv
                    .Where(x => x.ConversationId == c.Id)
                    .Any(x => ChatHub.IsUserOnline(x.UserId));
            }
            else
            {
                var otherUser = c.DirectConversation?.UserId1 == currentUserId
                    ? c.DirectConversation?.User2
                    : c.DirectConversation?.User1;
                otherUserId = otherUser?.Id;
                title = otherUser?.DisplayName ?? "Trò chuyện";
                avatarUrl = otherUser?.AvatarUrl;
                tagText = otherUser?.Role == "Admin" ? "Quản trị" : "Bạn bè";
                tagColor = otherUser?.Role == "Admin" ? "danger" : "info";
                isOnline = otherUserId.HasValue && ChatHub.IsUserOnline(otherUserId.Value);
            }

            var lastMsg = c.LastMessage;
            string lastContent = lastMsg == null
                ? ""
                : (lastMsg.IsRecalled ? "Tin nhắn đã được thu hồi" : (lastMsg.Content ?? ""));
            string preview = lastMsg != null
                ? (lastMsg.SenderId == currentUserId ? $"Bạn: {lastContent}" : lastContent)
                : "Chưa có tin nhắn";

            string formattedTime = "";
            if (lastMsg != null)
            {
                var diff = DateTimeOffset.UtcNow - lastMsg.CreatedAt;
                if (diff.TotalMinutes < 60)
                    formattedTime = $"{Math.Max(0, (int)diff.TotalMinutes)}m";
                else if (diff.TotalHours < 24)
                    formattedTime = $"{(int)diff.TotalHours}h";
                else
                    formattedTime = $"{(int)diff.TotalDays}d";
            }

            int unreadCount = unreadCounts.TryGetValue(c.Id, out var count) ? count : 0;

            convSummaries.Add(new ConversationSummaryDto
            {
                Id = c.Id,
                Title = title,
                AvatarUrl = avatarUrl,
                IsGroup = c.IsGroup,
                OtherUserId = otherUserId,
                LastMessageContent = preview,
                LastMessageTime = lastMsg?.CreatedAt,
                FormattedTime = formattedTime,
                TagText = tagText,
                TagColor = tagColor,
                IsActive = false,
                UnreadCount = unreadCount,
                IsOnline = isOnline
            });
        }

        // Bug #4 (CHAT-15): Không cho phép mở hội thoại không thuộc quyền thành viên
        if (conversationId.HasValue && convSummaries.All(cs => cs.Id != conversationId.Value))
        {
            return Forbid();
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
                activeSummary.UnreadCount = 0;
            }

            // Bug #6 (CHAT-05): Lấy 50 tin nhắn MỚI NHẤT rồi đảo lại theo thứ tự thời gian tăng dần
            var messages = await _context.Messages
                .Where(msg => msg.ConversationId == activeId.Value)
                .Include(msg => msg.Sender)
                .OrderByDescending(msg => msg.CreatedAt)
                .Take(50)
                .Select(msg => new MessageDto
                {
                    Id = msg.Id,
                    SenderId = msg.SenderId ?? Guid.Empty,
                    SenderName = msg.Sender != null ? msg.Sender.DisplayName : "Hệ thống",
                    SenderAvatar = msg.Sender != null ? msg.Sender.AvatarUrl : null,
                    Content = msg.IsRecalled ? "Tin nhắn đã được thu hồi" : (msg.Content ?? ""),
                    MessageType = msg.IsRecalled ? "Recalled" : msg.MessageType,
                    CreatedAt = msg.CreatedAt,
                    FormattedTime = msg.CreatedAt.ToString("HH:mm"),
                    IsSelf = msg.SenderId == currentUserId
                })
                .ToListAsync();
            messages.Reverse();

            var activeMembership = memberships.FirstOrDefault(m => m.ConversationId == activeId.Value);
            var latestMsgId = messages.LastOrDefault()?.Id;
            if (activeMembership != null && latestMsgId.HasValue && activeMembership.LastReadMessageId != latestMsgId)
            {
                activeMembership.LastReadMessageId = latestMsgId;
                activeMembership.LastReadAt = DateTimeOffset.UtcNow;
                await _context.SaveChangesAsync();
            }

            var members = await _context.ConversationMembers
                .Where(m => m.ConversationId == activeId.Value && m.LeftAt == null)
                .Include(m => m.User)
                .ToListAsync();

            var memberDtos = members.Select(m => new MemberDto
            {
                UserId = m.UserId,
                DisplayName = m.User.DisplayName,
                AvatarUrl = m.User.AvatarUrl,
                Role = m.Role,
                Bio = m.User.Bio ?? (m.Role == "Admin" ? "Trưởng nhóm" : "Thành viên"),
                IsOnline = ChatHub.IsUserOnline(m.UserId)
            }).ToList();

            var attachments = await _context.MessageAttachments
                .Where(a => a.Message.ConversationId == activeId.Value && !a.Message.IsRecalled)
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

            var activeConvEntity = await _context.Conversations.FindAsync(activeId.Value);

            activeConvDto = new ConversationDetailDto
            {
                Id = activeId.Value,
                Title = activeSummary?.Title ?? "Hội thoại",
                AvatarUrl = activeSummary?.AvatarUrl,
                IsGroup = activeConvEntity?.IsGroup ?? false,
                IsCurrentUserAdmin = activeMembership?.Role == "Admin",
                OtherUserId = activeSummary?.OtherUserId,
                IsOnline = activeSummary?.IsOnline ?? false,
                Messages = messages,
                Members = memberDtos,
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

        // Bug #4 (CHAT-15): Kiểm tra quyền thành viên trước khi trả dữ liệu
        var membership = await _context.ConversationMembers
            .FirstOrDefaultAsync(m => m.ConversationId == conversationId && m.UserId == currentUserId && m.LeftAt == null);
        if (membership == null)
        {
            return Forbid();
        }

        // Bug #6 (CHAT-05): Lấy 50 tin nhắn MỚI NHẤT rồi đảo lại theo thứ tự thời gian tăng dần
        var messages = await _context.Messages
            .Where(msg => msg.ConversationId == conversationId)
            .Include(msg => msg.Sender)
            .OrderByDescending(msg => msg.CreatedAt)
            .Take(50)
            .Select(msg => new MessageDto
            {
                Id = msg.Id,
                SenderId = msg.SenderId ?? Guid.Empty,
                SenderName = msg.Sender != null ? msg.Sender.DisplayName : "Hệ thống",
                SenderAvatar = msg.Sender != null ? msg.Sender.AvatarUrl : null,
                Content = msg.IsRecalled ? "Tin nhắn đã được thu hồi" : (msg.Content ?? ""),
                MessageType = msg.IsRecalled ? "Recalled" : msg.MessageType,
                CreatedAt = msg.CreatedAt,
                FormattedTime = msg.CreatedAt.ToString("HH:mm"),
                IsSelf = msg.SenderId == currentUserId
            })
            .ToListAsync();
        messages.Reverse();

        var latestMsgId = messages.LastOrDefault()?.Id;
        if (latestMsgId.HasValue && membership.LastReadMessageId != latestMsgId)
        {
            membership.LastReadMessageId = latestMsgId;
            membership.LastReadAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync();
        }

        var conv = await _context.Conversations
            .Include(c => c.DirectConversation)
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        var memberEntities = await _context.ConversationMembers
            .Where(m => m.ConversationId == conversationId && m.LeftAt == null)
            .Include(m => m.User)
            .ToListAsync();

        var members = memberEntities.Select(m => new MemberDto
        {
            UserId = m.UserId,
            DisplayName = m.User.DisplayName,
            AvatarUrl = m.User.AvatarUrl,
            Role = m.Role,
            Bio = m.User.Bio ?? (m.Role == "Admin" ? "Trưởng nhóm" : "Thành viên"),
            IsOnline = ChatHub.IsUserOnline(m.UserId)
        }).ToList();

        Guid? otherUserId = null;
        if (conv != null && !conv.IsGroup && conv.DirectConversation != null)
        {
            otherUserId = conv.DirectConversation.UserId1 == currentUserId
                ? conv.DirectConversation.UserId2
                : conv.DirectConversation.UserId1;
        }
        bool isOnline = otherUserId.HasValue
            ? ChatHub.IsUserOnline(otherUserId.Value)
            : members.Any(m => m.UserId != currentUserId && m.IsOnline);

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

        return Json(new
        {
            success = true,
            isGroup = conv?.IsGroup ?? false,
            isCurrentUserAdmin = membership.Role == "Admin",
            otherUserId,
            isOnline,
            messages,
            members,
            attachments
        });
    }

    [HttpPost]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest req)
    {
        var currentUserId = User.GetUserId();
        if (currentUserId == Guid.Empty || req == null || req.ConversationId == Guid.Empty)
        {
            return BadRequest(new { success = false, message = "Nội dung không hợp lệ" });
        }

        var result = await _chatService.SendTextMessageAsync(req.ConversationId, currentUserId, req.Content);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Json(result);
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
