using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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
    private readonly IAccountService _accountService;

    public AccountController(
        AppDbContext db,
        IPasswordHasher<User> passwordHasher,
        IFileStorageService fileStorageService,
        IWebHostEnvironment env,
        ILogger<AccountService> logger,
        IAccountService? accountService = null)
    {
        // Tự động dùng DI nếu Lead đã đăng ký trong Program.cs, hoặc tự khởi tạo ngay để chạy độc lập 100%
        _accountService = accountService ?? new AccountService(db, passwordHasher, fileStorageService, env, logger);
    }

    // 1. ĐĂNG KÝ (/Account/Register)
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Profile");
        }
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

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

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
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

        return RedirectToAction("Index", "Profile");
    }

    // 3. ĐĂNG XUẤT (/Account/Logout)
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
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
        return View();
    }

    // 5. QUÊN MẬT KHẨU (/Account/ForgotPassword & /Account/ResetPassword)
    [HttpGet]
    [AllowAnonymous]
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _accountService.GenerateForgotPasswordOtpAsync(model.EmailOrPhone);
        if (!result.Success || string.IsNullOrEmpty(result.Data))
        {
            ModelState.AddModelError(string.Empty, result.Message ?? "Không tìm thấy tài khoản.");
            return View(model);
        }

        TempData["DevOtpNotice"] = result.Message;
        return RedirectToAction(nameof(ResetPassword), new { email = result.Data });
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ResetPassword(string? email = null)
    {
        return View(new ResetPasswordViewModel { Email = email ?? string.Empty });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _accountService.ResetPasswordWithOtpAsync(model);
        if (!result.Success)
        {
            ModelState.AddModelError(nameof(model.OtpCode), result.Message ?? "Mã OTP không hợp lệ.");
            return View(model);
        }

        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Login));
    }

    // Giữ thêm đường dẫn /Account/Profile tự chuyển hướng sang /Profile/Index để tiện truy cập
    [HttpGet]
    [Authorize]
    public IActionResult Profile()
    {
        return RedirectToAction("Index", "Profile");
    }
}
