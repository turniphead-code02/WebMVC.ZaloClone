//** public class AdminService{} **//
using Microsoft.EntityFrameworkCore;
using ZaloClone.Data;

namespace ZaloClone.Services.Admin;
    // Khai báo cấu trúc dữ liệu dùng riêng cho Admin ngay tại đây
    public class AdminDashboardDto
    {
        public int TotalUsers { get; set; }
        public int TotalGroups { get; set; }
        public int TotalMessages { get; set; }
        public List<UserAdminDto> Users { get; set; } = new();
    }

    public class UserAdminDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty; // Khớp với cột display_name trong DB
        public bool IsLocked { get; set; }
    }

    public interface IAdminService
    {
        Task<AdminDashboardDto> GetDashboardDataAsync();
        Task ToggleLockUserAsync(Guid userId); // Đổi tên/gộp logic khóa hoặc mở khóa
    }

    public class AdminService : IAdminService
    {
        private readonly AppDbContext _context;

        public AdminService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<AdminDashboardDto> GetDashboardDataAsync()
        {
            // Thống kê số liệu thực tế từ Database
            int totalUsers = await _context.Users.CountAsync(u => !u.IsDeleted);
            int totalGroups = await _context.Conversations.CountAsync(c => c.IsGroup);
            int totalMessages = await _context.Messages.CountAsync();

            // Lấy danh sách user để hiển thị lên bảng quản trị
            var users = await _context.Users
                .Where(u => !u.IsDeleted)
                .OrderByDescending(u => u.CreatedAt)
                .Select(u => new UserAdminDto
                {
                    Id = u.Id,
                    Email = u.Email,
                    DisplayName = u.DisplayName,
                    IsLocked = u.IsLocked
                })
                .ToListAsync();

            return new AdminDashboardDto
            {
                TotalUsers = totalUsers,
                TotalGroups = totalGroups,
                TotalMessages = totalMessages,
                Users = users
            };
        }

        public async Task ToggleLockUserAsync(Guid userId)
        {
            // Logic cập nhật trạng thái khóa/mở khóa user trong DB
            var user = await _context.Users.FindAsync(userId);
            if (user != null && user.Role != "Admin")
            {
                user.IsLocked = !user.IsLocked;
                user.UpdatedAt = DateTimeOffset.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
    }
