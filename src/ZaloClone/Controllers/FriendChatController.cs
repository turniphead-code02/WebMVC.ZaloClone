using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZaloClone.Data;
using ZaloClone.Infrastructure;
using ZaloClone.Models.Entities;

namespace ZaloClone.Controllers;

[Authorize]
[Route("Friend")]
public class FriendChatController : Controller
{
    private readonly AppDbContext _db;

    public FriendChatController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost("StartChat")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartChat(Guid friendId)
    {
        var currentUserId = User.GetUserId();
        if (friendId == Guid.Empty || friendId == currentUserId)
        {
            return BadRequest(new { success = false, message = "Người nhận không hợp lệ." });
        }

        var pair = UserPair.Order(currentUserId, friendId);
        var areFriends = await _db.Friendships.AsNoTracking()
            .AnyAsync(f => f.UserId1 == pair.User1 && f.UserId2 == pair.User2);
        if (!areFriends)
        {
            return BadRequest(new { success = false, message = "Chỉ có thể nhắn tin với bạn bè." });
        }

        var isBlocked = await _db.UserBlocks.AsNoTracking()
            .AnyAsync(b => (b.BlockerId == currentUserId && b.BlockedId == friendId)
                        || (b.BlockerId == friendId && b.BlockedId == currentUserId));
        if (isBlocked)
        {
            return BadRequest(new { success = false, message = "Không thể nhắn tin do một trong hai người đã chặn người còn lại." });
        }

        var existingConversationId = await _db.DirectConversations.AsNoTracking()
            .Where(d => d.UserId1 == pair.User1 && d.UserId2 == pair.User2)
            .Select(d => (Guid?)d.ConversationId)
            .FirstOrDefaultAsync();

        if (existingConversationId.HasValue)
        {
            return Json(new
            {
                success = true,
                redirectUrl = Url.Action("Index", "Home", new { conversationId = existingConversationId.Value })
            });
        }

        var conversation = new Conversation
        {
            IsGroup = false,
            CreatedById = currentUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var directConversation = new DirectConversation
        {
            ConversationId = conversation.Id,
            IsGroup = false,
            UserId1 = pair.User1,
            UserId2 = pair.User2
        };

        _db.Conversations.Add(conversation);
        _db.DirectConversations.Add(directConversation);
        _db.ConversationMembers.AddRange(
            new ConversationMember { ConversationId = conversation.Id, UserId = currentUserId, Role = "Member" },
            new ConversationMember { ConversationId = conversation.Id, UserId = friendId, Role = "Member" });

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Một yêu cầu đồng thời có thể đã tạo cuộc trò chuyện cho cặp này.
            var concurrentConversationId = await _db.DirectConversations.AsNoTracking()
                .Where(d => d.UserId1 == pair.User1 && d.UserId2 == pair.User2)
                .Select(d => (Guid?)d.ConversationId)
                .FirstOrDefaultAsync();

            if (!concurrentConversationId.HasValue) throw;
            conversation.Id = concurrentConversationId.Value;
        }

        return Json(new
        {
            success = true,
            redirectUrl = Url.Action("Index", "Home", new { conversationId = conversation.Id })
        });
    }
}
