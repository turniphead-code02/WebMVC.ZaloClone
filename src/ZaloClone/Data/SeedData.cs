using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ZaloClone.Models.Entities;

namespace ZaloClone.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SeedData");

        // Kiểm tra nếu đã có users thì bỏ qua
        if (await context.Users.AnyAsync())
        {
            return;
        }

        logger.LogInformation("Bắt đầu khởi tạo dữ liệu mẫu (Seed Data)...");

        var users = new List<User>();

        // 1. Tài khoản Admin
        var admin = new User
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Email = "admin@zalo.local",
            PhoneNumber = "0900000001",
            DisplayName = "Quản Trị Viên",
            Role = "Admin",
            Bio = "Hệ thống quản trị Zalo Clone"
        };
        admin.PasswordHash = hasher.HashPassword(admin, "Test@123");
        users.Add(admin);

        // 2. Tài khoản User mẫu từ user1 đến user5
        var userNames = new[] { "Nguyễn Văn Huy", "Trần Đình Tùng", "Lê Tấn Tài", "Phạm Văn Hào", "Ngô Đăng Khôi" };
        for (int i = 0; i < userNames.Length; i++)
        {
            var userNum = i + 1;
            var user = new User
            {
                Id = Guid.Parse($"00000000-0000-0000-0000-00000000000{userNum + 1}"),
                Email = $"user{userNum}@zalo.local",
                PhoneNumber = $"090000000{userNum + 1}",
                DisplayName = userNames[i],
                Role = "User",
                Bio = $"Xin chào, tôi là {userNames[i]}"
            };
            user.PasswordHash = hasher.HashPassword(user, "Test@123");
            users.Add(user);
        }

        await context.Users.AddRangeAsync(users);
        await context.SaveChangesAsync();

        logger.LogInformation("Khởi tạo thành công {Count} tài khoản mẫu.", users.Count);
    }
}
