using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ZaloClone.Data;
using ZaloClone.Hubs;
using ZaloClone.Infrastructure;
using ZaloClone.Models.Entities;
using ZaloClone.Models.ViewModels.Friend;

namespace ZaloClone.Services.Friend;

public class FriendRequestService : IFriendRequestService
{
    private readonly AppDbContext _db;
    private readonly IHubContext<ChatHub> _hub;
    private readonly ILogger<FriendRequestService> _logger;

    public FriendRequestService(AppDbContext db, IHubContext<ChatHub> hub, ILogger<FriendRequestService> logger)
    {
        _db = db;
        _hub = hub;
        _logger = logger;
    }

    // 1. TÌM KIẾM NGƯỜI DÙNG (chính xác theo email hoặc số điện thoại)

    public async Task<ApiResponse<UserSearchResultViewModel>> SearchUserAsync(Guid currentUserId, string? keyword)
    {
        keyword = keyword?.Trim();
        if (string.IsNullOrEmpty(keyword))
        {
            return ApiResponse<UserSearchResultViewModel>.Fail("Vui lòng nhập số điện thoại hoặc email.");
        }

        IQueryable<User> query = _db.Users.AsNoTracking().Where(u => !u.IsDeleted && !u.IsLocked);

        if (keyword.Contains('@'))
        {
            var email = keyword.ToLowerInvariant();
            if (email.Length > 254 || !email.Contains('.'))
            {
                return ApiResponse<UserSearchResultViewModel>.Fail("Email không hợp lệ.");
            }

            query = query.Where(u => u.Email.ToLower() == email);
        }
        else
        {
            var phone = NormalizePhone(keyword);
            if (phone == null)
            {
                return ApiResponse<UserSearchResultViewModel>.Fail("Số điện thoại không hợp lệ (chỉ gồm 9-11 chữ số).");
            }

            query = query.Where(u => u.PhoneNumber == phone);
        }

        var target = await query
            .Select(u => new { u.Id, u.DisplayName, u.AvatarUrl, u.Bio })
            .FirstOrDefaultAsync();

        const string notFound = "Không tìm thấy người dùng.";

        if (target == null)
        {
            return ApiResponse<UserSearchResultViewModel>.Fail(notFound);
        }

        if (target.Id == currentUserId)
        {
            return ApiResponse<UserSearchResultViewModel>.Fail("Đây là tài khoản của bạn.");
        }

        // Người kia đã chặn mình -> giả vờ như không tồn tại (bảo vệ quyền riêng tư)
        if (await _db.UserBlocks.AnyAsync(b => b.BlockerId == target.Id && b.BlockedId == currentUserId))
        {
            return ApiResponse<UserSearchResultViewModel>.Fail(notFound);
        }

        var result = new UserSearchResultViewModel
        {
            UserId = target.Id,
            DisplayName = target.DisplayName,
            AvatarUrl = target.AvatarUrl,
            Bio = target.Bio,
            RelationStatus = FriendRelation.None
        };

        if (await _db.UserBlocks.AnyAsync(b => b.BlockerId == currentUserId && b.BlockedId == target.Id))
        {
            result.RelationStatus = FriendRelation.Blocked;
            return ApiResponse<UserSearchResultViewModel>.Ok(result);
        }

        var (u1, u2) = UserPair.Order(currentUserId, target.Id);
        if (await _db.Friendships.AnyAsync(f => f.UserId1 == u1 && f.UserId2 == u2))
        {
            result.RelationStatus = FriendRelation.Friend;
            return ApiResponse<UserSearchResultViewModel>.Ok(result);
        }

        var pending = await _db.FriendRequests.AsNoTracking()
            .Where(r => r.Status == "Pending" &&
                        ((r.SenderId == currentUserId && r.ReceiverId == target.Id) ||
                         (r.SenderId == target.Id && r.ReceiverId == currentUserId)))
            .Select(r => new { r.Id, r.SenderId })
            .FirstOrDefaultAsync();

        if (pending != null)
        {
            result.RequestId = pending.Id;
            result.RelationStatus = pending.SenderId == currentUserId
                ? FriendRelation.RequestSent
                : FriendRelation.RequestReceived;
        }

        return ApiResponse<UserSearchResultViewModel>.Ok(result);
    }


    // 2. GỬI LỜI MỜI KẾT BẠN

    public async Task<ApiResponse<SentFriendRequestViewModel>> SendRequestAsync(Guid currentUserId, Guid receiverId)
    {
        if (receiverId == Guid.Empty)
        {
            return ApiResponse<SentFriendRequestViewModel>.Fail("Người nhận không hợp lệ.");
        }

        if (receiverId == currentUserId)
        {
            return ApiResponse<SentFriendRequestViewModel>.Fail("Bạn không thể tự kết bạn với chính mình.");
        }

        var receiver = await _db.Users.AsNoTracking()
            .Where(u => u.Id == receiverId && !u.IsDeleted && !u.IsLocked)
            .Select(u => new { u.Id, u.DisplayName, u.AvatarUrl })
            .FirstOrDefaultAsync();
        if (receiver == null)
        {
            return ApiResponse<SentFriendRequestViewModel>.Fail("Người dùng không tồn tại.");
        }

        if (await _db.UserBlocks.AnyAsync(b =>
                (b.BlockerId == receiverId && b.BlockedId == currentUserId) ||
                (b.BlockerId == currentUserId && b.BlockedId == receiverId)))
        {
            return ApiResponse<SentFriendRequestViewModel>.Fail("Không thể gửi lời mời kết bạn cho người dùng này.");
        }

        var (u1, u2) = UserPair.Order(currentUserId, receiverId);
        if (await _db.Friendships.AnyAsync(f => f.UserId1 == u1 && f.UserId2 == u2))
        {
            return ApiResponse<SentFriendRequestViewModel>.Fail("Hai bạn đã là bạn bè.");
        }

        var existing = await _db.FriendRequests.AsNoTracking()
            .Where(r => r.Status == "Pending" &&
                        ((r.SenderId == currentUserId && r.ReceiverId == receiverId) ||
                         (r.SenderId == receiverId && r.ReceiverId == currentUserId)))
            .Select(r => r.SenderId)
            .FirstOrDefaultAsync();

        // Guid.Empty nghĩa là không có lời mời nào đang chờ
        if (existing == currentUserId)
        {
            return ApiResponse<SentFriendRequestViewModel>.Fail("Bạn đã gửi lời mời cho người này rồi.");
        }

        if (existing == receiverId)
        {
            return ApiResponse<SentFriendRequestViewModel>.Fail("Người này đã gửi lời mời cho bạn. Hãy vào danh sách lời mời để chấp nhận.");
        }

        var request = new FriendRequest
        {
            SenderId = currentUserId,
            ReceiverId = receiverId,
            Status = "Pending"
        };

        _db.FriendRequests.Add(request);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // Trường hợp 2 người cùng bấm gửi một lúc -> vi phạm unique index uq_pending_friend_request
            _logger.LogWarning(ex, "Gửi lời mời kết bạn thất bại {Sender} -> {Receiver}", currentUserId, receiverId);
            return ApiResponse<SentFriendRequestViewModel>.Fail("Không thể gửi lời mời. Vui lòng thử lại.");
        }

        await NotifyReceiverAsync(request);

        return ApiResponse<SentFriendRequestViewModel>.Ok(new SentFriendRequestViewModel
        {
            RequestId = request.Id,
            ReceiverId = receiver.Id,
            ReceiverName = receiver.DisplayName,
            ReceiverAvatarUrl = receiver.AvatarUrl,
            CreatedAt = request.CreatedAt
        }, "Đã gửi lời mời kết bạn.");
    }


    // 3. HỦY LỜI MỜI ĐÃ GỬI

    public async Task<ApiResponse> CancelRequestAsync(Guid currentUserId, Guid requestId)
    {
        // Kiểm tra quyền sở hữu: chỉ người GỬI mới được hủy
        var request = await _db.FriendRequests
            .FirstOrDefaultAsync(r => r.Id == requestId && r.SenderId == currentUserId);

        if (request == null)
        {
            return ApiResponse.Fail("Không tìm thấy lời mời kết bạn.");
        }

        if (request.Status != "Pending")
        {
            return ApiResponse.Fail("Lời mời này đã được xử lý, không thể hủy.");
        }

        request.Status = "Cancelled";
        request.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return ApiResponse.Ok("Đã hủy lời mời kết bạn.");
    }


    // 4. DANH SÁCH LỜI MỜI ĐÃ GỬI (đang chờ)

    public Task<List<SentFriendRequestViewModel>> GetSentRequestsAsync(Guid currentUserId)
    {
        return _db.FriendRequests.AsNoTracking()
            .Where(r => r.SenderId == currentUserId && r.Status == "Pending")
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new SentFriendRequestViewModel
            {
                RequestId = r.Id,
                ReceiverId = r.ReceiverId,
                ReceiverName = r.Receiver.DisplayName,
                ReceiverAvatarUrl = r.Receiver.AvatarUrl,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();
    }
    // 5. DANH SÁCH BẠN BÈ
    public async Task<List<FriendListItemViewModel>> GetFriendsAsync(Guid currentUserId)
    {
        var friendIds = await _db.Friendships.AsNoTracking()
            .Where(f => f.UserId1 == currentUserId || f.UserId2 == currentUserId)
            .Select(f => f.UserId1 == currentUserId ? f.UserId2 : f.UserId1)
            .ToListAsync();

        return await _db.Users.AsNoTracking()
            .Where(u => friendIds.Contains(u.Id) && !u.IsDeleted && !u.IsLocked)
            .OrderBy(u => u.DisplayName)
            .Select(u => new FriendListItemViewModel
            {
                UserId = u.Id,
                DisplayName = u.DisplayName,
                AvatarUrl = u.AvatarUrl
            })
            .ToListAsync();
    }

    // 6. DANH SÁCH LỜI MỜI ĐANG CHỜ DUYỆT
    public async Task<List<IncomingFriendRequestViewModel>> GetIncomingRequestsAsync(
        Guid currentUserId)
    {
        return await _db.FriendRequests.AsNoTracking()
            .Where(r => r.ReceiverId == currentUserId && r.Status == "Pending")
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new IncomingFriendRequestViewModel
            {
                RequestId = r.Id,
                UserId = r.SenderId,
                DisplayName = r.Sender.DisplayName,
                AvatarUrl = r.Sender.AvatarUrl
            })
            .ToListAsync();
    }

    // 7. CHẤP NHẬN LỜI MỜI
    public async Task<ApiResponse> AcceptRequestAsync(Guid currentUserId, Guid requestId)
    {
        var request = await _db.FriendRequests
            .FirstOrDefaultAsync(r =>
                r.Id == requestId &&
                r.ReceiverId == currentUserId &&
                r.Status == "Pending");

        if (request == null)
        {
            return ApiResponse.Fail("Không tìm thấy lời mời đang chờ duyệt.");
        }

        var blocked = await _db.UserBlocks.AnyAsync(b =>
            (b.BlockerId == currentUserId && b.BlockedId == request.SenderId) ||
            (b.BlockerId == request.SenderId && b.BlockedId == currentUserId));

        if (blocked)
        {
            return ApiResponse.Fail("Không thể chấp nhận lời mời này.");
        }

        var (userId1, userId2) = UserPair.Order(currentUserId, request.SenderId);

        if (!await _db.Friendships.AnyAsync(f =>
                f.UserId1 == userId1 && f.UserId2 == userId2))
        {
            _db.Friendships.Add(new Friendship
            {
                UserId1 = userId1,
                UserId2 = userId2
            });
        }

        request.Status = "Accepted";
        request.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return ApiResponse.Ok("Đã chấp nhận lời mời kết bạn.");
    }

    // 8. TỪ CHỐI LỜI MỜI
    public async Task<ApiResponse> RejectRequestAsync(Guid currentUserId, Guid requestId)
    {
        var request = await _db.FriendRequests
            .FirstOrDefaultAsync(r =>
                r.Id == requestId &&
                r.ReceiverId == currentUserId &&
                r.Status == "Pending");

        if (request == null)
        {
            return ApiResponse.Fail("Không tìm thấy lời mời đang chờ duyệt.");
        }

        request.Status = "Rejected";
        request.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return ApiResponse.Ok("Đã từ chối lời mời kết bạn.");
    }

    // 9. CHẶN NGƯỜI DÙNG
    public async Task<ApiResponse> BlockAsync(Guid currentUserId, Guid friendId)
    {
        if (friendId == Guid.Empty || friendId == currentUserId)
        {
            return ApiResponse.Fail("Người cần chặn không hợp lệ.");
        }

        var userExists = await _db.Users.AnyAsync(u =>
            u.Id == friendId && !u.IsDeleted);

        if (!userExists)
        {
            return ApiResponse.Fail("Không tìm thấy người dùng.");
        }

        var alreadyBlocked = await _db.UserBlocks.AnyAsync(b =>
            b.BlockerId == currentUserId && b.BlockedId == friendId);

        if (alreadyBlocked)
        {
            return ApiResponse.Ok("Người dùng này đã bị chặn.");
        }

        _db.UserBlocks.Add(new UserBlock
        {
            BlockerId = currentUserId,
            BlockedId = friendId
        });

        var (userId1, userId2) = UserPair.Order(currentUserId, friendId);
        var friendship = await _db.Friendships.FirstOrDefaultAsync(f =>
            f.UserId1 == userId1 && f.UserId2 == userId2);

        if (friendship != null)
        {
            _db.Friendships.Remove(friendship);
        }

        var pendingRequests = await _db.FriendRequests
            .Where(r => r.Status == "Pending" &&
                ((r.SenderId == currentUserId && r.ReceiverId == friendId) ||
                 (r.SenderId == friendId && r.ReceiverId == currentUserId)))
            .ToListAsync();

        foreach (var request in pendingRequests)
        {
            request.Status = "Cancelled";
            request.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync();
        return ApiResponse.Ok("Đã chặn người dùng.");
    }

    // 10. MỞ HOẶC TẠO CHAT 1-1 VỚI BẠN BÈ
    public async Task<ApiResponse<DirectChatResultViewModel>> GetOrCreateDirectConversationAsync(Guid currentUserId, Guid friendId)
    {
        if (friendId == Guid.Empty || friendId == currentUserId)
        {
            return ApiResponse<DirectChatResultViewModel>.Fail("Người nhận không hợp lệ.");
        }

        var friendExists = await _db.Users.AsNoTracking()
            .AnyAsync(u => u.Id == friendId && !u.IsDeleted && !u.IsLocked);
        if (!friendExists)
        {
            return ApiResponse<DirectChatResultViewModel>.Fail("Người dùng không tồn tại hoặc đã bị khóa.");
        }

        var (u1, u2) = UserPair.Order(currentUserId, friendId);

        // Chỉ được nhắn tin với người đã là bạn bè (kiểm tra ở server, không tin nút bấm ở giao diện)
        if (!await _db.Friendships.AnyAsync(f => f.UserId1 == u1 && f.UserId2 == u2))
        {
            return ApiResponse<DirectChatResultViewModel>.Fail("Hai bạn chưa là bạn bè.");
        }

        // Có chặn (một trong hai chiều) thì không mở được chat
        var blocked = await _db.UserBlocks.AnyAsync(b =>
            (b.BlockerId == currentUserId && b.BlockedId == friendId) ||
            (b.BlockerId == friendId && b.BlockedId == currentUserId));
        if (blocked)
        {
            return ApiResponse<DirectChatResultViewModel>.Fail("Không thể nhắn tin cho người dùng này.");
        }

        var conversationId = await FindDirectConversationIdAsync(u1, u2);

        if (conversationId == null)
        {
            // Chưa có -> tạo mới: 1 Conversation + 1 DirectConversation + 2 ConversationMember (cùng 1 lần lưu)
            var conversation = new Conversation
            {
                IsGroup = false,
                CreatedById = currentUserId
            };

            _db.Conversations.Add(conversation);
            _db.DirectConversations.Add(new DirectConversation
            {
                ConversationId = conversation.Id,
                IsGroup = false,
                UserId1 = u1,
                UserId2 = u2
            });
            _db.ConversationMembers.Add(new ConversationMember
            {
                ConversationId = conversation.Id,
                UserId = currentUserId,
                Role = "Member"
            });
            _db.ConversationMembers.Add(new ConversationMember
            {
                ConversationId = conversation.Id,
                UserId = friendId,
                Role = "Member"
            });

            try
            {
                await _db.SaveChangesAsync();
                conversationId = conversation.Id;
            }
            catch (DbUpdateException ex)
            {
                // Hai người cùng bấm "Nhắn tin" một lúc: unique uq_direct_conversations_pair chặn bản thứ hai.
                // Bỏ bản nháp vừa thêm rồi lấy lại hội thoại đã được người kia tạo.
                _logger.LogWarning(ex, "Tạo chat 1-1 trùng giữa {A} và {B}, lấy lại hội thoại có sẵn", u1, u2);
                _db.ChangeTracker.Clear();
                conversationId = await FindDirectConversationIdAsync(u1, u2);

                if (conversationId == null)
                {
                    return ApiResponse<DirectChatResultViewModel>.Fail("Không thể tạo cuộc trò chuyện. Vui lòng thử lại.");
                }
            }
        }
        else
        {
            await EnsureDirectMembersAsync(conversationId.Value, currentUserId, friendId);
        }

        return ApiResponse<DirectChatResultViewModel>.Ok(new DirectChatResultViewModel
        {
            ConversationId = conversationId.Value
        });
    }

    // 11. (GIAI ĐOẠN 2 - TASK 2) ĐẾM LỜI MỜI ĐẾN ĐANG CHỜ -> số trên badge icon Bạn bè
    public Task<int> CountIncomingPendingAsync(Guid currentUserId)
    {
        return _db.FriendRequests.AsNoTracking()
            .CountAsync(r => r.ReceiverId == currentUserId && r.Status == "Pending");
    }

    // Helpers

    private Task<Guid?> FindDirectConversationIdAsync(Guid userId1, Guid userId2)
    {
        return _db.DirectConversations.AsNoTracking()
            .Where(d => d.UserId1 == userId1 && d.UserId2 == userId2)
            .Select(d => (Guid?)d.ConversationId)
            .FirstOrDefaultAsync();
    }

    // Bảo đảm cả hai người đều là thành viên đang hoạt động của hội thoại 1-1 đã có
    private async Task EnsureDirectMembersAsync(Guid conversationId, Guid userA, Guid userB)
    {
        var members = await _db.ConversationMembers
            .Where(m => m.ConversationId == conversationId && (m.UserId == userA || m.UserId == userB))
            .ToListAsync();

        var changed = false;
        foreach (var uid in new[] { userA, userB })
        {
            var member = members.FirstOrDefault(m => m.UserId == uid);
            if (member == null)
            {
                _db.ConversationMembers.Add(new ConversationMember
                {
                    ConversationId = conversationId,
                    UserId = uid,
                    Role = "Member"
                });
                changed = true;
            }
            else if (member.LeftAt != null)
            {
                member.LeftAt = null;
                changed = true;
            }
        }

        if (!changed)
        {
            return;
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Không cập nhật được thành viên hội thoại {ConversationId}", conversationId);
        }
    }

    // Task 2: người nhận lời mời vừa chấp nhận -> báo cho NGƯỜI GỬI (sự kiện FriendshipChanged theo CONVENTIONS mục 7)
    private async Task NotifyRequestAcceptedAsync(Guid senderId, Guid accepterId)
    {
        try
        {
            var accepter = await _db.Users.AsNoTracking()
                .Where(u => u.Id == accepterId)
                .Select(u => new { u.DisplayName, u.AvatarUrl })
                .FirstAsync();

            await _hub.Clients.Group(ChatHub.GetUserGroupName(senderId))
                .SendAsync("FriendshipChanged", new
                {
                    userId = accepterId,
                    action = "accepted",
                    displayName = accepter.DisplayName,
                    avatarUrl = accepter.AvatarUrl
                });
        }
        catch (Exception ex)
        {
            // Lỗi real-time không được làm hỏng việc chấp nhận kết bạn (đã lưu DB)
            _logger.LogWarning(ex, "Không đẩy được FriendshipChanged tới {Sender}", senderId);
        }
    }


    // Helpers

    private async Task NotifyReceiverAsync(FriendRequest request)
    {
        try
        {
            var sender = await _db.Users.AsNoTracking()
                .Where(u => u.Id == request.SenderId)
                .Select(u => new { u.DisplayName, u.AvatarUrl })
                .FirstAsync();

            await _hub.Clients.Group(ChatHub.GetUserGroupName(request.ReceiverId))
                .SendAsync("FriendRequestReceived", new
                {
                    requestId = request.Id,
                    senderId = request.SenderId,
                    senderName = sender.DisplayName,
                    senderAvatarUrl = sender.AvatarUrl,
                    createdAt = request.CreatedAt
                });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không đẩy được FriendRequestReceived tới {Receiver}", request.ReceiverId);
        }
    }

    //Chuẩn hóa SĐT: bỏ khoảng trắng/dấu, +84xxxxxxxxx -> 0xxxxxxxxx. Trả null nếu không hợp lệ.
    private static string? NormalizePhone(string input)
    {
        var digits = new string(input.Where(c => char.IsDigit(c) || c == '+').ToArray());
        if (digits.StartsWith("+84"))
        {
            digits = "0" + digits[3..];
        }
        else if (digits.StartsWith("84") && digits.Length >= 11)
        {
            digits = "0" + digits[2..];
        }

        if (digits.Any(c => !char.IsDigit(c)))
        {
            return null;
        }

        return digits.Length is >= 9 and <= 11 ? digits : null;
    }
}
