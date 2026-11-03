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
    public async Task<IActionResult> Search()
    {
        var vm = new FriendSearchPageViewModel
        {
            SentRequests = await _friendRequestService.GetSentRequestsAsync(User.GetUserId())
        };
        return View(vm);
    }

    // GET /Friend/FindUser?keyword=...   (AJAX)
    [HttpGet]
    public async Task<IActionResult> FindUser(string? keyword)
    {
        var result = await _friendRequestService.SearchUserAsync(User.GetUserId(), keyword);
        return Json(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendRequest(Guid receiverId)
    {
        var result = await _friendRequestService.SendRequestAsync(User.GetUserId(), receiverId);
        return Json(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelRequest(Guid requestId)
    {
        var result = await _friendRequestService.CancelRequestAsync(User.GetUserId(), requestId);
        return Json(result);
    }

    //Phần của hào (danh sách bạn bè, chấp nhận/từ chối, chặn) thêm bên dưới
}

