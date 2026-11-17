#pragma warning disable IDE1006, IDE0290, IDE0305, IDE0028, IDE0031, IDE0057, CA1853, CA1854, CA1862
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
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
/// Phụ trách: Xem &amp; sửa thông tin cá nhân, Đổi mật khẩu (xoay SecurityStamp), Cập nhật &amp; Xóa Avatar (Task 1), Đặt biệt danh thân mật.
/// </summary>
[Authorize]
public class ProfileController : Controller
{
    private readonly IAccountService _accountService;
    private readonly IWebHostEnvironment _env;

    public ProfileController(
        AppDbContext db,
        IPasswordHasher<User> passwordHasher,
        IFileStorageService fileStorageService,
        IWebHostEnvironment env,
        ILogger<AccountService>? logger = null,
        IAccountService? accountService = null)
    {
        _env = env;
        _accountService = accountService ?? new AccountService(db, passwordHasher, fileStorageService, env);
    }

    // 1. XEM TRANG CÁ NHÂN (GET: /Profile hoặc /Profile/Index)
    [HttpGet]
    public async Task<IActionResult> Index(Guid? id = null)
    {
        var currentUserId = User.GetUserId();
        if (currentUserId == Guid.Empty)
        {
            return RedirectToAction("Login", "Account");
        }

        var targetUserId = id.HasValue && id.Value != Guid.Empty ? id.Value : currentUserId;
        var vm = await _accountService.GetProfileAsync(targetUserId, currentUserId);
        if (vm == null)
        {
            if (targetUserId == currentUserId)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction("Login", "Account");
            }

            return RedirectToAction(nameof(Index));
        }

        return View("~/Views/Profile/Index.cshtml", vm);
    }

    // 2. SỬA THÔNG TIN CÁ NHÂN (POST: /Profile/EditProfile)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditProfile(ProfileViewModel model)
    {
        var userId = User.GetUserId();
        var result = await _accountService.UpdateProfileAsync(userId, model);
        if (!result.Success || result.Data == null)
        {
            TempData["ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        await _accountService.SignInUserAsync(HttpContext, result.Data, isPersistent: true);
        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    // 3. CẬP NHẬT & XÓA ẢNH ĐẠI DIỆN AVATAR (TASK 1 - ĐỒNG BỘ 4 CỘT)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAvatar(IFormFile? avatarFile)
    {
        var userId = User.GetUserId();
        var result = await _accountService.UpdateAvatarAsync(userId, avatarFile);
        if (!result.Success || result.Data == null)
        {
            TempData["ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        await _accountService.SignInUserAsync(HttpContext, result.Data, isPersistent: true);
        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAvatar()
    {
        var userId = User.GetUserId();
        var result = await _accountService.DeleteAvatarAsync(userId);
        if (result.Success && result.Data != null)
        {
            await _accountService.SignInUserAsync(HttpContext, result.Data, isPersistent: true);
            TempData["SuccessMessage"] = result.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    // 4. ĐỔI MẬT KHẨU & XOAY SECURITYSTAMP (TASK 2 - ĐĂNG XUẤT TRÌNH DUYỆT KHÁC)
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
            TempData["ErrorMessage"] = "Vui lòng kiểm tra lại thông tin mật khẩu.";
            return View(model);
        }

        var userId = User.GetUserId();
        var result = await _accountService.ChangePasswordAsync(userId, model);
        if (!result.Success || result.Data == null)
        {
            TempData["ErrorMessage"] = result.Message;
            ModelState.AddModelError(nameof(model.CurrentPassword), result.Message ?? "Đổi mật khẩu thất bại.");
            return View(model);
        }

        await _accountService.SignInUserAsync(HttpContext, result.Data, isPersistent: true);
        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    // 5. ĐẶT BIỆT DANH THÂN MẬT CHO NGƯỜI KHÁC (POST: /Profile/SaveNickname)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SaveNickname(Guid targetUserId, string? nickname, Guid? returnConvId = null)
    {
        var currentUserId = User.GetUserId();
        var webRoot = string.IsNullOrWhiteSpace(_env.WebRootPath)
            ? Path.Combine(_env.ContentRootPath, "wwwroot")
            : _env.WebRootPath;
        var dir = Path.Combine(webRoot, "uploads", "profiles");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"nicknames_{currentUserId:N}.json");

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (System.IO.File.Exists(path))
        {
            try
            {
                map = JsonSerializer.Deserialize<Dictionary<string, string>>(System.IO.File.ReadAllText(path))
                      ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            catch { }
        }

        if (string.IsNullOrWhiteSpace(nickname))
        {
            map.Remove(targetUserId.ToString());
            TempData["SuccessMessage"] = "Đã khôi phục tên gốc.";
        }
        else
        {
            map[targetUserId.ToString()] = nickname.Trim();
            TempData["SuccessMessage"] = "Đã đổi biệt danh thành \"" + nickname.Trim() + "\"!";
        }

        System.IO.File.WriteAllText(path, JsonSerializer.Serialize(map));

        if (returnConvId.HasValue && returnConvId.Value != Guid.Empty)
        {
            return Redirect("/?conversationId=" + returnConvId.Value);
        }

        return RedirectToAction(nameof(Index), new { id = targetUserId });
    }
}
