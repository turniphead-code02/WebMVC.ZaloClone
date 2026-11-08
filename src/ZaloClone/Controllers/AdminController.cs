//namespace ZaloClone.Controllers;
//*public class AdminController{}*//

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZaloClone.Data;
using ZaloClone.Hubs;

namespace ZaloClone.Controllers;

[Authorize(Roles = "Admin")]
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
    public async Task<IActionResult> Admin(string searchTerm = "", string statusFilter = "All", int page = 1)
    {
        int pageSize = 5; // Số dòng trên mỗi trang

        // Thống kê trực tiếp
        ViewBag.TotalUsers = await _context.Users.CountAsync(u => !u.IsDeleted);
        ViewBag.TotalGroups = await _context.Conversations.CountAsync(c => c.IsGroup);
        ViewBag.TotalMessages = await _context.Messages.CountAsync();

        var query = _context.Users
            .Where(u => !u.IsDeleted)
            .AsQueryable();

        // Tìm kiếm theo Email hoặc Tên hiển thị
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            string searchKey = searchTerm.Trim().ToLower();
            query = query.Where(u => u.Email.ToLower().Contains(searchKey) || u.DisplayName.ToLower().Contains(searchKey));
        }

        // Lọc trạng thái
        if (statusFilter == "Active")
        {
            query = query.Where(u => !u.IsLocked);
        }
        else if (statusFilter == "Locked")
        {
            query = query.Where(u => u.IsLocked);
        }

        // Phân trang
        int totalUsersCount = await query.CountAsync();
        int totalPages = (int)Math.Ceiling(totalUsersCount / (double)pageSize);
        if (totalPages < 1) totalPages = 1;
        if (page < 1) page = 1;
        if (page > totalPages) page = totalPages;

        var pagedUsers = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.SearchTerm = searchTerm;
        ViewBag.StatusFilter = statusFilter;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;

        return View(pagedUsers);
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

            await _hubContext.Clients.User(userId.ToString()).SendAsync("ForceLogout", new { reason = "Tài khoản của bạn đã bị khóa." });
        }

        return RedirectToAction(nameof(Admin));
    }
}
