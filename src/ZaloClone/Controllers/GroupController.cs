using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ZaloClone.Infrastructure;
using ZaloClone.Services.Group;

namespace ZaloClone.Controllers;

[Authorize]
public class GroupController : Controller
{
    private readonly IGroupService _groupService;

    public GroupController(IGroupService groupService)
    {
        _groupService = groupService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromForm] CreateGroupRequest request)
    {
        var userId = User.GetUserId();
        if (request == null || string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(ApiResponse.Fail("Tên nhóm không hợp lệ."));
        }

        var memberIds = request.MemberIds ?? new List<Guid>();
        var result = await _groupService.CreateGroupAsync(request.Title, userId, memberIds, request.Avatar);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Json(result);
    }

    [HttpPost]
    public async Task<IActionResult> AddMembers([FromBody] AddMembersRequest request)
    {
        var userId = User.GetUserId();
        if (request == null || request.GroupId == Guid.Empty || request.MemberIds == null || request.MemberIds.Count == 0)
        {
            return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ."));
        }

        var result = await _groupService.AddMembersAsync(request.GroupId, userId, request.MemberIds);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Json(result);
    }

    [HttpPost]
    public async Task<IActionResult> RemoveMember([FromBody] RemoveMemberRequest request)
    {
        var userId = User.GetUserId();
        if (request == null || request.GroupId == Guid.Empty || request.MemberId == Guid.Empty)
        {
            return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ."));
        }

        var result = await _groupService.RemoveMemberAsync(request.GroupId, userId, request.MemberId);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Json(result);
    }

    [HttpPost]
    public async Task<IActionResult> Leave([FromBody] LeaveGroupRequest request)
    {
        var userId = User.GetUserId();
        if (request == null || request.GroupId == Guid.Empty)
        {
            return BadRequest(ApiResponse.Fail("Mã nhóm không hợp lệ."));
        }

        var result = await _groupService.LeaveGroupAsync(request.GroupId, userId);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Json(result);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid groupId)
    {
        var userId = User.GetUserId();
        var result = await _groupService.GetGroupDetailsAsync(groupId, userId);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Json(result);
    }

    [HttpGet]
    public async Task<IActionResult> FriendsToInvite(Guid? groupId = null)
    {
        var userId = User.GetUserId();
        var result = await _groupService.GetFriendsForGroupInviteAsync(userId, groupId);
        return Json(result);
    }
}

public class CreateGroupRequest
{
    public string Title { get; set; } = string.Empty;
    public List<Guid>? MemberIds { get; set; }
    public IFormFile? Avatar { get; set; }
}

public class AddMembersRequest
{
    public Guid GroupId { get; set; }
    public List<Guid> MemberIds { get; set; } = new();
}

public class RemoveMemberRequest
{
    public Guid GroupId { get; set; }
    public Guid MemberId { get; set; }
}

public class LeaveGroupRequest
{
    public Guid GroupId { get; set; }
}
