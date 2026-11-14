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

    // GET: /Admin/Admin (Quản lý người dùng)
    public async Task<IActionResult> Admin(string searchTerm = "", string statusFilter = "All", int page = 1)
    {
        int pageSize = 5;

        ViewBag.TotalUsers = await _context.Users.CountAsync(u => !u.IsDeleted);
        ViewBag.TotalGroups = await _context.Conversations.CountAsync(c => c.IsGroup);
        ViewBag.TotalMessages = await _context.Messages.CountAsync();

        var query = _context.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            string searchKey = searchTerm.Trim().ToLower();
            query = query.Where(u => u.Email.ToLower().Contains(searchKey) || u.DisplayName.ToLower().Contains(searchKey));
        }

        if (statusFilter == "Active")
        {
            query = query.Where(u => !u.IsDeleted && !u.IsLocked);
        }
        else if (statusFilter == "Locked")
        {
            query = query.Where(u => !u.IsDeleted && u.IsLocked);
        }
        else if (statusFilter == "Deleted")
        {
            query = query.Where(u => u.IsDeleted);
        }

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

    // GET: /Admin/ManageGroups (Quản lý nhóm chat)
    // GET: /Admin/ManageGroups
    public async Task<IActionResult> ManageGroups(string searchGroup = "", string groupStatusFilter = "All", int page = 1)
    {
        int pageSize = 5;

        ViewBag.TotalUsers = await _context.Users.CountAsync(u => !u.IsDeleted);
        ViewBag.TotalGroups = await _context.Conversations.CountAsync(c => c.IsGroup);
        ViewBag.TotalMessages = await _context.Messages.CountAsync();

        var query = _context.Conversations
            .Include(c => c.Members)
            .Include(c => c.CreatedBy) // Nạp thêm thông tin người tạo nhóm
            .Where(c => c.IsGroup)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchGroup))
        {
            string searchKey = searchGroup.Trim().ToLower();
            query = query.Where(c => c.Title != null && c.Title.ToLower().Contains(searchKey));
        }

        if (groupStatusFilter == "Active")
        {
            query = query.Where(c => c.Members.Any());
        }
        else if (groupStatusFilter == "Inactive")
        {
            query = query.Where(c => !c.Members.Any());
        }

        int totalGroupsCount = await query.CountAsync();
        int totalPages = (int)Math.Ceiling(totalGroupsCount / (double)pageSize);
        if (totalPages < 1) totalPages = 1;
        if (page < 1) page = 1;
        if (page > totalPages) page = totalPages;

        var pagedGroups = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.SearchGroup = searchGroup;
        ViewBag.GroupStatusFilter = groupStatusFilter;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;

        return View("ManageGroups", pagedGroups);
    }

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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleDelete(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user != null && user.Role != "Admin")
        {
            user.IsDeleted = !user.IsDeleted;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync();

            if (user.IsDeleted)
            {
                await _hubContext.Clients.User(userId.ToString()).SendAsync("ForceLogout", new { reason = "Tài khoản của bạn đã bị xóa." });
            }
        }

        return RedirectToAction(nameof(Admin));
    }
}
