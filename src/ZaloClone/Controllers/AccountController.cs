#pragma warning disable IDE1006, IDE0290, IDE0305, IDE0028, IDE0031, IDE0057, CA1853, CA1862
using System;
using System.IO;
using System.Linq;
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
/// Phụ trách: Đăng ký, Đăng nhập, Đăng xuất, Quên mật khẩu (OTP), Từ chối truy cập, Kiểm tra SecurityStamp, Quick Login (Dev).
/// </summary>
public class AccountController : Controller
{
    private readonly AppDbContext _db;
    private readonly IAccountService _accountService;
    private readonly IWebHostEnvironment _env;

    public AccountController(
        AppDbContext db,
        IPasswordHasher<User> passwordHasher,
        IFileStorageService fileStorageService,
        IWebHostEnvironment env,
        ILogger<AccountService>? logger = null,
        IAccountService? accountService = null)
    {
        _db = db;
        _env = env;
        _accountService = accountService ?? new AccountService(db, passwordHasher, fileStorageService, env);
    }

    private string ResolveAccountView(string pascalName, string lowerName)
    {
        var lowerPath = Path.Combine(_env.ContentRootPath, "Views", "Account", lowerName + ".cshtml");
        if (System.IO.File.Exists(lowerPath))
        {
            return $"~/Views/Account/{lowerName}.cshtml";
        }

        return $"~/Views/Account/{pascalName}.cshtml";
    }

    // 1. ĐĂNG KÝ (/Account/Register)
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
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
            TempData["ErrorMessage"] = "Vui lòng kiểm tra lại thông tin đăng ký.";
            return View(model);
        }

        var result = await _accountService.RegisterAsync(model);
        if (!result.Success || result.Data == null)
        {
            TempData["ErrorMessage"] = result.Message ?? "Đăng ký không thành công.";
            ModelState.AddModelError(string.Empty, result.Message ?? "Đăng ký không thành công.");
            return View(model);
        }

        await _accountService.SignInUserAsync(HttpContext, result.Data, isPersistent: false);
        TempData["SuccessMessage"] = "Đăng ký tài khoản thành công! Chào mừng bạn đến với ZaloClone.";
        return RedirectToAction("Index", "Home");
    }

    // 2. ĐĂNG NHẬP (/Account/Login)
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Login(string? returnUrl = null, bool expired = false)
    {
        if (expired)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["ErrorMessage"] = "Phiên đăng nhập đã hết hiệu lực do mật khẩu vừa được thay đổi ở trình duyệt khác. Vui lòng đăng nhập lại!";
        }
        else if (User.Identity?.IsAuthenticated == true)
        {
            return User.IsAdmin() ? RedirectToAction("Index", "Admin") : LocalRedirect(returnUrl ?? "/");
        }
        else if (Request.Cookies.ContainsKey("ZaloClone.Auth") && !string.IsNullOrEmpty(returnUrl))
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["ErrorMessage"] = "Phiên đăng nhập đã hết hiệu lực do mật khẩu vừa được thay đổi ở trình duyệt khác. Vui lòng đăng nhập lại!";
        }

        ViewBag.ReturnUrl = returnUrl;
        ViewBag.SampleUsers = await _db.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted && !u.IsLocked)
            .OrderBy(u => u.Email)
            .Take(6)
            .ToListAsync();

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Vui lòng nhập đầy đủ thông tin đăng nhập.";
            ViewBag.SampleUsers = await _db.Users
                .AsNoTracking()
                .Where(u => !u.IsDeleted && !u.IsLocked)
                .OrderBy(u => u.Email)
                .Take(6)
                .ToListAsync();
            return View(model);
        }

        var result = await _accountService.ValidateLoginAsync(model);
        if (!result.Success || result.Data == null)
        {
            TempData["ErrorMessage"] = result.Message ?? "Đăng nhập thất bại.";
            ModelState.AddModelError(string.Empty, result.Message ?? "Đăng nhập thất bại.");
            ViewBag.SampleUsers = await _db.Users
                .AsNoTracking()
                .Where(u => !u.IsDeleted && !u.IsLocked)
                .OrderBy(u => u.Email)
                .Take(6)
                .ToListAsync();
            return View(model);
        }

        await _accountService.SignInUserAsync(HttpContext, result.Data, model.RememberMe);
        TempData["SuccessMessage"] = result.Message;

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return string.Equals(result.Data.Role, "Admin", StringComparison.OrdinalIgnoreCase)
            ? RedirectToAction("Index", "Admin")
            : LocalRedirect("/");
    }

    // QuickLogin cho môi trường Dev & Test
    [HttpGet]
    [HttpPost]
    public async Task<IActionResult> QuickLogin(Guid userId, string? returnUrl = null)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted && !u.IsLocked);
        if (user == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy tài khoản.";
            return RedirectToAction(nameof(Login));
        }

        await _accountService.SignInUserAsync(HttpContext, user, isPersistent: true);
        return LocalRedirect(returnUrl ?? "/");
    }

    // 3. ĐĂNG XUẤT (/Account/Logout)
    [AllowAnonymous]
    [AcceptVerbs("GET", "POST")]
    public async Task<IActionResult> Logout()
    {
        var userId = User.GetUserId();
        if (userId != Guid.Empty)
        {
            await _accountService.UpdateLastActiveAsync(userId);
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["SuccessMessage"] = "Bạn đã đăng xuất thành công.";
        return RedirectToAction(nameof(Login));
    }

    // 4. TỪ CHỐI TRUY CẬP (/Account/AccessDenied)
    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View(ResolveAccountView("AccessDenied", "Accessdenied"));
    }

    // 5. QUÊN MẬT KHẨU (/Account/ForgotPassword & /Account/ResetPassword)
    [HttpGet]
    [AllowAnonymous]
    public IActionResult ForgotPassword()
    {
        return View(ResolveAccountView("ForgotPassword", "Forgotpassword"), new ForgotPasswordViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        var viewPath = ResolveAccountView("ForgotPassword", "Forgotpassword");
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Vui lòng nhập Email hoặc Số điện thoại.";
            return View(viewPath, model);
        }

        var result = await _accountService.GenerateForgotPasswordOtpAsync(model.EmailOrPhone);
        if (!result.Success || string.IsNullOrEmpty(result.Data))
        {
            TempData["ErrorMessage"] = result.Message;
            ModelState.AddModelError(string.Empty, result.Message ?? "Không tìm thấy tài khoản.");
            return View(viewPath, model);
        }

        TempData["DevOtpNotice"] = result.Message;
        TempData["SuccessMessage"] = "Đã tạo mã xác thực OTP khôi phục mật khẩu!";
        return RedirectToAction(nameof(ResetPassword), new { email = result.Data });
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ResetPassword(string? email = null)
    {
        return View(ResolveAccountView("ResetPassword", "Resetpassword"), new ResetPasswordViewModel { Email = email ?? string.Empty });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        var viewPath = ResolveAccountView("ResetPassword", "Resetpassword");
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Vui lòng kiểm tra lại mã OTP và mật khẩu mới.";
            return View(viewPath, model);
        }

        var result = await _accountService.ResetPasswordWithOtpAsync(model);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.Message;
            ModelState.AddModelError(nameof(model.OtpCode), result.Message ?? "Mã OTP không hợp lệ.");
            return View(viewPath, model);
        }

        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Login));
    }

    // 6. KIỂM TRA SECURITYSTAMP THỜI GIAN THỰC (/Account/CheckSession)
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> CheckSession()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Json(new { valid = false });
        }

        var userId = User.GetUserId();
        var claimStamp = User.FindFirst("SecurityStamp")?.Value;
        if (userId == Guid.Empty || string.IsNullOrEmpty(claimStamp))
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Json(new { valid = false });
        }

        var dbUser = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId && !u.IsDeleted && !u.IsLocked)
            .Select(u => new { u.SecurityStamp })
            .FirstOrDefaultAsync();

        if (dbUser == null || !string.Equals(claimStamp, dbUser.SecurityStamp.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Json(new { valid = false });
        }

        return Json(new { valid = true });
    }

    // 7. CHUYỂN HƯỚNG TRANG CÁ NHÂN (/Account/Profile)
    [HttpGet]
    [Authorize]
    public IActionResult Profile(Guid? id = null)
    {
        return RedirectToAction("Index", "Profile", new { id });
    }
}
