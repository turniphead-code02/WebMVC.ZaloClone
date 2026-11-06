using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZaloClone.Infrastructure;
using ZaloClone.Models.ViewModels.Friend;
using ZaloClone.Services.Friend;

namespace ZaloClone.Controllers;

[Authorize]
public class FriendController : Controller
{
    private readonly IFriendRequestService _friendRequestService;

    public FriendController(IFriendRequestService friendRequestService)
    {
        _friendRequestService = friendRequestService;
    }

    //Tìm kiếm + Gửi/Hủy lời mời

    // GET /Friend/Search
    [HttpGet]
    public async Task<IActionResult> SearchAsync()
    {
        var vm = new FriendSearchPageViewModel
        {
            SentRequests = await _friendRequestService.GetSentRequestsAsync(User.GetUserId())
        };
        return View(vm);
    }

    // GET /Friend/FindUser?keyword=...   (AJAX)
    [HttpGet]
    public async Task<IActionResult> FindUserAsync(string? keyword)
    {
        var result = await _friendRequestService.SearchUserAsync(User.GetUserId(), keyword);
        return Json(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendRequestAsync(Guid receiverId)
    {
        var result = await _friendRequestService.SendRequestAsync(User.GetUserId(), receiverId);
        return Json(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelRequestAsync(Guid requestId)
    {
        var result = await _friendRequestService.CancelRequestAsync(User.GetUserId(), requestId);
        return Json(result);
    }

    [HttpGet]
    public async Task<IActionResult> ListAsync()
    {
        var friends = await _friendRequestService.GetFriendsAsync(User.GetUserId());
        return View("List", friends);
    }

    // Xem các lời mời kết bạn đang chờ duyệt
    // GET /Friend/Requests
    [HttpGet]
    public async Task<IActionResult> RequestsAsync()
    {
        var requests = await _friendRequestService.GetIncomingRequestsAsync(User.GetUserId());
        return View("Requests", requests);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptRequestAsync(Guid requestId)
    {
        var result = await _friendRequestService.AcceptRequestAsync(User.GetUserId(), requestId);
        return Json(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectRequestAsync(Guid requestId)
    {
        var result = await _friendRequestService.RejectRequestAsync(User.GetUserId(), requestId);
        return Json(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BlockAsync(Guid friendId)
    {
        var result = await _friendRequestService.BlockAsync(User.GetUserId(), friendId);
        return Json(result);
    }
}
