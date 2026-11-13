using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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
/// Đặt tại: src/ZaloClone/Controllers/ProfileController.cs
/// Phụ trách: Xem &amp; sửa thông tin cá nhân, Đổi mật khẩu, Cập nhật &amp; Kiểm tra lưu ảnh đại diện (Avatar).
/// Tự động khởi tạo AccountService mà KHÔNG cần sửa Program.cs.
/// </summary>
[Authorize]
public class ProfileController : Controller
{
    private readonly IAccountService _accountService;

    public ProfileController(
        AppDbContext db,
        IPasswordHasher<User> passwordHasher,
        IFileStorageService fileStorageService,
        IWebHostEnvironment env,
        ILogger<AccountService> logger,
        IAccountService? accountService = null)
    {
        _accountService = accountService ?? new AccountService(db, passwordHasher, fileStorageService, env, logger);
    }

    // 1. XEM TRANG CÁ NHÂN (GET: /Profile hoặc /Profile/Index)
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty)
        {
            return RedirectToAction("Login", "Account");
        }

        var vm = await _accountService.GetProfileAsync(userId);
        if (vm == null)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }

        return View(vm);
    }

    // 2. SỬA THÔNG TIN CÁ NHÂN (POST: /Profile/EditProfile)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditProfile(ProfileViewModel model)
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty)
        {
            return RedirectToAction("Login", "Account");
        }

        var result = await _accountService.UpdateProfileAsync(userId, model);
        if (!result.Success || result.Data == null)
        {
            TempData["ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        // Cập nhật lại Cookie Claims để tên hiển thị mới có hiệu lực ngay
        await _accountService.SignInUserAsync(HttpContext, result.Data, isPersistent: true);
        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    // 3. CẬP NHẬT & KIỂM TRA LƯU ẢNH ĐẠI DIỆN AVATAR (POST: /Profile/UpdateAvatar)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAvatar(IFormFile? avatarFile)
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty)
        {
            return RedirectToAction("Login", "Account");
        }

        var result = await _accountService.UpdateAvatarAsync(userId, avatarFile);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        await _accountService.SignInUserAsync(HttpContext, result.Data.User, isPersistent: true);
        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    // 4. ĐỔI MẬT KHẨU (GET & POST: /Profile/ChangePassword)
    [HttpGet]
    public IActionResult ChangePassword()
    {
        return View(new ChangePasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userId = User.GetUserId();
        var result = await _accountService.ChangePasswordAsync(userId, model);
        if (!result.Success || result.Data == null)
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), result.Message ?? "Đổi mật khẩu thất bại.");
            return View(model);
        }

        // Ký lại Cookie với SecurityStamp mới cho phiên hiện tại
        await _accountService.SignInUserAsync(HttpContext, result.Data, isPersistent: true);
        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    // 5. ENDPOINT KIỂM TRA NHANH AVATAR & USERID (GET: /Profile/VerifyAvatarAndAuth)
    [HttpGet]
    public async Task<IActionResult> VerifyAvatarAndAuth()
    {
        var userId = User.GetUserId();
        var profile = await _accountService.GetProfileAsync(userId);
        if (profile == null)
        {
            return Json(ApiResponse<object>.Fail("Không tìm thấy người dùng."));
        }

        return Json(ApiResponse<object>.Ok(new
        {
            userId = profile.Id,
            getUserIdExtensionResult = User.GetUserId(),
            displayName = profile.DisplayName,
            email = profile.Email,
            phoneNumber = profile.PhoneNumber,
            role = profile.Role,
            avatarUrlInDb = profile.AvatarUrl,
            avatarExistsOnDisk = profile.AvatarFileExistsOnDisk,
            avatarFileSizeBytes = profile.AvatarFileSizeBytes
        }, "Kiểm tra xác thực Cookie & lưu trữ Avatar thành công."));
    }
}
