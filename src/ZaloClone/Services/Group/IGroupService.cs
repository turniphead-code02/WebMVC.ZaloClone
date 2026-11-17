using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using ZaloClone.Infrastructure;

namespace ZaloClone.Services.Group;

public interface IGroupService
{
    Task<ApiResponse<GroupDetailDto>> CreateGroupAsync(string title, Guid creatorId, List<Guid> memberIds, IFormFile? avatarFile = null);
    Task<ApiResponse> AddMembersAsync(Guid groupId, Guid actorId, List<Guid> newMemberIds);
    Task<ApiResponse> RemoveMemberAsync(Guid groupId, Guid actorId, Guid memberId);
    Task<ApiResponse> LeaveGroupAsync(Guid groupId, Guid memberId);
    Task<ApiResponse<GroupDetailDto>> GetGroupDetailsAsync(Guid groupId, Guid userId);
    Task<ApiResponse<List<FriendOptionDto>>> GetFriendsForGroupInviteAsync(Guid userId, Guid? groupId = null);
}

public class GroupDetailDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public int MemberCount { get; set; }
    public Guid CreatedById { get; set; }
    public List<GroupMemberDto> Members { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; }
}

public class GroupMemberDto
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = "Member";
    public DateTimeOffset JoinedAt { get; set; }
}

public class FriendOptionDto
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public bool IsAlreadyMember { get; set; }
}
