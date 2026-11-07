//namespace ZaloClone.Controllers;
//*public class AdminController{}*//

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZaloClone.Data;
using ZaloClone.Hubs;

namespace ZaloClone.Controllers;

//[Authorize(Roles = "Admin")]  // Note để vô Trang Quản Trị Admin mà ko cần login của Admin
public class AdminController : Controller
{
    private readonly AppDbContext _context;
    private readonly IHubContext<ChatHub> _hubContext;

    public AdminController(AppDbContext context, IHubContext<ChatHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    // GET: /Admin/Admin
    public async Task<IActionResult> Admin()
    {
        // Thống kê trực tiếp bằng LINQ
        ViewBag.TotalUsers = await _context.Users.CountAsync(u => !u.IsDeleted);
        ViewBag.TotalGroups = await _context.Conversations.CountAsync(c => c.IsGroup);
        ViewBag.TotalMessages = await _context.Messages.CountAsync();

        // Lấy trực tiếp danh sách User truyền ra View
        var users = await _context.Users
            .Where(u => !u.IsDeleted)
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

        return View(users);
    }

    // POST: /Admin/ToggleLock
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLock(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user != null && user.Role != "Admin")
        {
            user.IsLocked = !user.IsLocked;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync();

            // Bắn tín hiệu ForceLogout qua SignalR nếu user đang đăng nhập
            await _hubContext.Clients.User(userId.ToString()).SendAsync("ForceLogout", new { reason = "Tài khoản của bạn đã bị khóa." });
        }

        return RedirectToAction(nameof(Admin));
    }
}
