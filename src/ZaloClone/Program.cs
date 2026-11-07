using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ZaloClone.Data;
using ZaloClone.Hubs;
using ZaloClone.Infrastructure;
using ZaloClone.Models.Entities;
using ZaloClone.Services.Friend;
using ZaloClone.Services.Storage;

var builder = WebApplication.CreateBuilder(args);

// 1. Cấu hình Database (PostgreSQL + Snake Case)
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Port=5432;Database=ZaloCloneDb;Username=postgres;Password=YourSecurePassword2026";

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString)
           .UseSnakeCaseNamingConvention();
});

// 2. Cấu hình Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "ZaloClone.Auth";
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;

        // Kiểm tra tài khoản bị khóa / đổi mật khẩu mỗi request
        options.Events.OnValidatePrincipal = async context =>
        {
            var userId = context.Principal?.GetUserId() ?? Guid.Empty;
            if (userId == Guid.Empty)
            {
                context.RejectPrincipal();
                return;
            }

            var dbContext = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var user = await dbContext.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.IsLocked, u.IsDeleted, u.SecurityStamp })
                .FirstOrDefaultAsync();

            if (user == null || user.IsDeleted || user.IsLocked)
            {
                context.RejectPrincipal();
                return;
            }

            var claimStamp = context.Principal?.FindFirstValue("SecurityStamp");
            if (claimStamp != null && claimStamp != user.SecurityStamp.ToString())
            {
                context.RejectPrincipal();
            }
        };
    });

builder.Services.AddAuthorization();

// 3. Đăng ký Services dùng chung
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();

// Cặp 2: Dịch vụ bạn bè & lời mời kết bạn
builder.Services.AddScoped<IFriendRequestService, FriendRequestService>();

// 4. SignalR Real-time
builder.Services.AddSignalR();

// 5. MVC Views & Controllers
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Tự động Seed Data khi ở môi trường Development
if (app.Environment.IsDevelopment())
{
    try
    {
        await SeedData.InitializeAsync(app.Services);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Không thể seed dữ liệu khởi tạo (có thể chưa chạy migration)");
    }
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // Đảm bảo phục vụ file upload trong wwwroot/uploads
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Endpoint SignalR
app.MapHub<ChatHub>("/hubs/chat");

app.Run();
