using System.Security.Claims;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZaloClone.Data;
using ZaloClone.Infrastructure;
using ZaloClone.Models.Entities;
using ZaloClone.Services.Account;
using ZaloClone.Services.Storage;

namespace ZaloClone.Controllers;

/// <summary>
/// Đặt tại: src/ZaloClone/Controllers/AccountController.cs
/// Phụ trách: Đăng ký, Đăng nhập, Đăng xuất, Quên mật khẩu (OTP), Từ chối truy cập.
/// Tự động khởi tạo AccountService mà KHÔNG cần sửa Program.cs.
/// </summary>
public class AccountController : Controller
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _hasher;
    private readonly IAccountService _accountService;

    public AccountController(AppDbContext db, IPasswordHasher<User> hasher)
    public AccountController(
        AppDbContext db,
        IPasswordHasher<User> passwordHasher,
        IFileStorageService fileStorageService,
        IWebHostEnvironment env,
        ILogger<AccountService> logger,
        IAccountService? accountService = null)
    {
        _db = db;
        _hasher = hasher;
        // Tự động dùng DI nếu Lead đã đăng ký trong Program.cs, hoặc tự khởi tạo ngay để chạy độc lập 100%
        _accountService = accountService ?? new AccountService(db, passwordHasher, fileStorageService, env, logger);
    }

    // 1. ĐĂNG KÝ (/Account/Register)
    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl = null)
    [AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(returnUrl ?? "/");
            return RedirectToAction("Index", "Profile");
        }
        return View(new RegisterViewModel());
    }

        ViewBag.ReturnUrl = returnUrl;
        var sampleUsers = await _db.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted && !u.IsLocked)
            .OrderBy(u => u.Email)
            .Take(6)
            .ToListAsync();
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        return View(sampleUsers);
        var result = await _accountService.RegisterAsync(model);
        if (!result.Success || result.Data == null)
        {
            ModelState.AddModelError(string.Empty, result.Message ?? "Đăng ký không thành công.");
            return View(model);
    }

        await _accountService.SignInUserAsync(HttpContext, result.Data, isPersistent: false);
        TempData["SuccessMessage"] = "Đăng ký tài khoản thành công! Chào mừng bạn đến với ZaloClone.";
        return RedirectToAction("Index", "Profile");
    }

    // 2. ĐĂNG NHẬP (/Account/Login)
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Profile");
        }
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpGet]
    [HttpPost]
    public async Task<IActionResult> QuickLogin(Guid userId, string? returnUrl = null)
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted && !u.IsLocked);
        if (user == null)
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Không tìm thấy tài khoản.";
            return RedirectToAction(nameof(Login));
            return View(model);
        }

        var result = await _accountService.ValidateLoginAsync(model);
        if (!result.Success || result.Data == null)
        {
            ModelState.AddModelError(string.Empty, result.Message ?? "Đăng nhập thất bại.");
            return View(model);
        }

        await _accountService.SignInUserAsync(HttpContext, result.Data, model.RememberMe);

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        await SignInUserAsync(user);
        return LocalRedirect(returnUrl ?? "/Friend/Search");
        return RedirectToAction("Index", "Profile");
        return LocalRedirect(returnUrl ?? "/");
    }

    // 3. ĐĂNG XUẤT (/Account/Logout)
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
    public async Task<IActionResult> Logout()
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        await _accountService.UpdateLastActiveAsync(User.GetUserId());
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["SuccessMessage"] = "Bạn đã đăng xuất thành công.";
        return RedirectToAction(nameof(Login));
    }

    // 4. TỪ CHỐI TRUY CẬP (/Account/AccessDenied)
    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
        {
            ModelState.AddModelError("", "Vui lòng nhập email và mật khẩu.");
            return await Login(returnUrl);
        return View();
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail && !u.IsDeleted);
    // 5. QUÊN MẬT KHẨU (/Account/ForgotPassword & /Account/ResetPassword)
    [HttpGet]
    [AllowAnonymous]
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordViewModel());
    }

        if (user == null || user.IsLocked)
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ModelState.AddModelError("", "Tài khoản hoặc mật khẩu không chính xác.");
            return await Login(returnUrl);
            return View(model);
        }

        var verifyResult = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verifyResult == PasswordVerificationResult.Failed)
        var result = await _accountService.GenerateForgotPasswordOtpAsync(model.EmailOrPhone);
        if (!result.Success || string.IsNullOrEmpty(result.Data))
        {
            ModelState.AddModelError("", "Tài khoản hoặc mật khẩu không chính xác.");
            return await Login(returnUrl);
            ModelState.AddModelError(string.Empty, result.Message ?? "Không tìm thấy tài khoản.");
            return View(model);
        }

        await SignInUserAsync(user);
        return LocalRedirect(returnUrl ?? "/");
        TempData["DevOtpNotice"] = result.Message;
        return RedirectToAction(nameof(ResetPassword), new { email = result.Data });
    }

    [HttpGet]
    public async Task<IActionResult> Logout()
    [AllowAnonymous]
    public IActionResult ResetPassword(string? email = null)
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
        return View(new ResetPasswordViewModel { Email = email ?? string.Empty });
    }

    private async Task SignInUserAsync(User user)
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
    {
        var claims = new List<Claim>
            return View(model);
        }

        var result = await _accountService.ResetPasswordWithOtpAsync(model);
        if (!result.Success)
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new("SecurityStamp", user.SecurityStamp.ToString())
        };
            ModelState.AddModelError(nameof(model.OtpCode), result.Message ?? "Mã OTP không hợp lệ.");
            return View(model);
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Login));
    }

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
    // Giữ thêm đường dẫn /Account/Profile tự chuyển hướng sang /Profile/Index để tiện truy cập
    [HttpGet]
    [Authorize]
    public IActionResult Profile()
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
        });
        return RedirectToAction("Index", "Profile");
    }
}
