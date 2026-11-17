using System;
using System.Collections.Generic;
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
using ZaloClone.Services.Storage;

namespace ZaloClone.Services.Group;

public class GroupService : IGroupService
{
    private readonly AppDbContext _context;
    private readonly IFileStorageService _storageService;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<GroupService> _logger;

    public GroupService(
        AppDbContext context,
        IFileStorageService storageService,
        IHubContext<ChatHub> hubContext,
        ILogger<GroupService> logger)
    {
        _context = context;
        _storageService = storageService;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<ApiResponse<GroupDetailDto>> CreateGroupAsync(string title, Guid creatorId, List<Guid> memberIds, IFormFile? avatarFile = null)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return ApiResponse<GroupDetailDto>.Fail("Tên nhóm không được để trống.");
        }

        var creator = await _context.Users.FindAsync(creatorId);
        if (creator == null)
        {
            return ApiResponse<GroupDetailDto>.Fail("Người tạo nhóm không hợp lệ.");
        }

        string? avatarUrl = null;
        if (avatarFile != null && avatarFile.Length > 0)
        {
            try
            {
                var storageKey = await _storageService.SaveFileAsync(avatarFile, "groups");
                avatarUrl = _storageService.GetRelativeUrl(storageKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể lưu ảnh đại diện nhóm: {Message}", ex.Message);
            }
        }

        var now = DateTimeOffset.UtcNow;
        var group = new Conversation
        {
            Id = Guid.NewGuid(),
            IsGroup = true,
            Title = title.Trim(),
            AvatarUrl = avatarUrl,
            CreatedById = creatorId,
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.Conversations.Add(group);

        // Thêm người tạo làm Trưởng nhóm (Admin)
        var members = new List<ConversationMember>
        {
            new ConversationMember
            {
                Id = Guid.NewGuid(),
                ConversationId = group.Id,
                UserId = creatorId,
                Role = "Admin",
                JoinedAt = now
            }
        };

        // Thêm các thành viên được chọn
        var validMemberIds = memberIds.Distinct().Where(id => id != creatorId).ToList();
        var existingUsers = await _context.Users
            .Where(u => validMemberIds.Contains(u.Id) && !u.IsDeleted)
            .ToListAsync();

        foreach (var user in existingUsers)
        {
            members.Add(new ConversationMember
            {
                Id = Guid.NewGuid(),
                ConversationId = group.Id,
                UserId = user.Id,
                Role = "Member",
                JoinedAt = now
            });
        }

        _context.ConversationMembers.AddRange(members);

        // Tin nhắn hệ thống thông báo tạo nhóm
        var sysMsg = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = group.Id,
            SenderId = creatorId,
            Content = $"{creator.DisplayName} đã tạo nhóm \"{group.Title}\"",
            MessageType = "System",
            CreatedAt = now
        };
        _context.Messages.Add(sysMsg);
        group.LastMessageId = sysMsg.Id;

        await _context.SaveChangesAsync();

        var detail = new GroupDetailDto
        {
            Id = group.Id,
            Title = group.Title,
            AvatarUrl = group.AvatarUrl,
            MemberCount = members.Count,
            CreatedById = creatorId,
            CreatedAt = group.CreatedAt,
            Members = members.Select(m => new GroupMemberDto
            {
                UserId = m.UserId,
                DisplayName = m.UserId == creatorId ? creator.DisplayName : existingUsers.First(u => u.Id == m.UserId).DisplayName,
                AvatarUrl = m.UserId == creatorId ? creator.AvatarUrl : existingUsers.First(u => u.Id == m.UserId).AvatarUrl,
                Role = m.Role,
                JoinedAt = m.JoinedAt
            }).ToList()
        };

        // Thông báo cho các thành viên qua SignalR
        foreach (var m in members)
        {
            await _hubContext.Clients.Group(ChatHub.GetUserGroupName(m.UserId))
                .SendAsync("GroupCreated", new
                {
                    groupId = group.Id,
                    title = group.Title,
                    avatarUrl = group.AvatarUrl
                });
        }

        return ApiResponse<GroupDetailDto>.Ok(detail, "Tạo nhóm thành công.");
    }

    public async Task<ApiResponse> AddMembersAsync(Guid groupId, Guid actorId, List<Guid> newMemberIds)
    {
        var group = await _context.Conversations.FirstOrDefaultAsync(c => c.Id == groupId && c.IsGroup);
        if (group == null)
        {
            return ApiResponse.Fail("Không tìm thấy nhóm trò chuyện.");
        }

        var isActorMember = await _context.ConversationMembers
            .AnyAsync(m => m.ConversationId == groupId && m.UserId == actorId && m.LeftAt == null);
        if (!isActorMember)
        {
            return ApiResponse.Fail("Bạn không phải thành viên của nhóm này.");
        }

        var actor = await _context.Users.FindAsync(actorId);
        var currentMemberIds = await _context.ConversationMembers
            .Where(m => m.ConversationId == groupId && m.LeftAt == null)
            .Select(m => m.UserId)
            .ToListAsync();

        var toAddIds = newMemberIds.Distinct().Except(currentMemberIds).ToList();
        if (!toAddIds.Any())
        {
            return ApiResponse.Fail("Các thành viên này đã có trong nhóm.");
        }

        var usersToAdd = await _context.Users
            .Where(u => toAddIds.Contains(u.Id) && !u.IsDeleted)
            .ToListAsync();

        var now = DateTimeOffset.UtcNow;
        foreach (var user in usersToAdd)
        {
            _context.ConversationMembers.Add(new ConversationMember
            {
                Id = Guid.NewGuid(),
                ConversationId = groupId,
                UserId = user.Id,
                Role = "Member",
                JoinedAt = now
            });
        }

        var names = string.Join(", ", usersToAdd.Select(u => u.DisplayName));
        var sysMsg = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = groupId,
            SenderId = actorId,
            Content = $"{actor?.DisplayName ?? "Thành viên"} đã thêm {names} vào nhóm.",
            MessageType = "System",
            CreatedAt = now
        };
        _context.Messages.Add(sysMsg);
        group.LastMessageId = sysMsg.Id;
        group.UpdatedAt = now;

        await _context.SaveChangesAsync();

        await _hubContext.Clients.Group(ChatHub.GetConversationGroupName(groupId))
            .SendAsync("ReceiveMessage", new
            {
                id = sysMsg.Id,
                conversationId = groupId,
                senderId = actorId,
                senderName = "Hệ thống",
                content = sysMsg.Content,
                messageType = "System",
                createdAt = sysMsg.CreatedAt.ToString("HH:mm")
            });

        return ApiResponse.Ok("Đã thêm thành viên vào nhóm.");
    }

    public async Task<ApiResponse> RemoveMemberAsync(Guid groupId, Guid actorId, Guid memberId)
    {
        var group = await _context.Conversations.FirstOrDefaultAsync(c => c.Id == groupId && c.IsGroup);
        if (group == null) return ApiResponse.Fail("Không tìm thấy nhóm.");

        var actorMember = await _context.ConversationMembers
            .FirstOrDefaultAsync(m => m.ConversationId == groupId && m.UserId == actorId && m.LeftAt == null);
        if (actorMember == null || actorMember.Role != "Admin")
        {
            return ApiResponse.Fail("Chỉ trưởng nhóm mới có quyền xóa thành viên.");
        }

        var targetMember = await _context.ConversationMembers
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.ConversationId == groupId && m.UserId == memberId && m.LeftAt == null);

        if (targetMember == null)
        {
            return ApiResponse.Fail("Thành viên không tồn tại trong nhóm.");
        }

        targetMember.LeftAt = DateTimeOffset.UtcNow;

        var sysMsg = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = groupId,
            SenderId = actorId,
            Content = $"{targetMember.User.DisplayName} đã bị xóa khỏi nhóm.",
            MessageType = "System",
            CreatedAt = DateTimeOffset.UtcNow
        };
        _context.Messages.Add(sysMsg);
        group.LastMessageId = sysMsg.Id;

        await _context.SaveChangesAsync();
        return ApiResponse.Ok("Đã xóa thành viên khỏi nhóm.");
    }

    public async Task<ApiResponse> LeaveGroupAsync(Guid groupId, Guid memberId)
    {
        var member = await _context.ConversationMembers
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.ConversationId == groupId && m.UserId == memberId && m.LeftAt == null);

        if (member == null) return ApiResponse.Fail("Bạn không phải thành viên nhóm này.");

        member.LeftAt = DateTimeOffset.UtcNow;

        var sysMsg = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = groupId,
            SenderId = memberId,
            Content = $"{member.User.DisplayName} đã rời khỏi nhóm.",
            MessageType = "System",
            CreatedAt = DateTimeOffset.UtcNow
        };
        _context.Messages.Add(sysMsg);

        await _context.SaveChangesAsync();
        return ApiResponse.Ok("Bạn đã rời khỏi nhóm.");
    }

    public async Task<ApiResponse<GroupDetailDto>> GetGroupDetailsAsync(Guid groupId, Guid userId)
    {
        var isMember = await _context.ConversationMembers
            .AnyAsync(m => m.ConversationId == groupId && m.UserId == userId && m.LeftAt == null);
        if (!isMember) return ApiResponse<GroupDetailDto>.Fail("Không có quyền truy cập.");

        var group = await _context.Conversations
            .Include(c => c.Members.Where(m => m.LeftAt == null))
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(c => c.Id == groupId && c.IsGroup);

        if (group == null) return ApiResponse<GroupDetailDto>.Fail("Không tìm thấy nhóm.");

        var dto = new GroupDetailDto
        {
            Id = group.Id,
            Title = group.Title,
            AvatarUrl = group.AvatarUrl,
            MemberCount = group.Members.Count,
            CreatedById = group.CreatedById ?? Guid.Empty,
            CreatedAt = group.CreatedAt,
            Members = group.Members.Select(m => new GroupMemberDto
            {
                UserId = m.UserId,
                DisplayName = m.User.DisplayName,
                AvatarUrl = m.User.AvatarUrl,
                Role = m.Role,
                JoinedAt = m.JoinedAt
            }).ToList()
        };

        return ApiResponse<GroupDetailDto>.Ok(dto);
    }

    public async Task<ApiResponse<List<FriendOptionDto>>> GetFriendsForGroupInviteAsync(Guid userId, Guid? groupId = null)
    {
        // Lấy danh sách bạn bè
        var friendships = await _context.Friendships
            .Where(f => f.UserId1 == userId || f.UserId2 == userId)
            .Include(f => f.User1)
            .Include(f => f.User2)
            .ToListAsync();

        var friends = friendships.Select(f => f.UserId1 == userId ? f.User2 : f.User1).ToList();

        HashSet<Guid> currentMembers = new();
        if (groupId.HasValue)
        {
            currentMembers = (await _context.ConversationMembers
                .Where(m => m.ConversationId == groupId.Value && m.LeftAt == null)
                .Select(m => m.UserId)
                .ToListAsync()).ToHashSet();
        }

        var result = friends.Select(f => new FriendOptionDto
        {
            UserId = f.Id,
            DisplayName = f.DisplayName,
            Email = f.Email,
            AvatarUrl = f.AvatarUrl,
            IsAlreadyMember = currentMembers.Contains(f.Id)
        }).ToList();

        return ApiResponse<List<FriendOptionDto>>.Ok(result);
    }
}
