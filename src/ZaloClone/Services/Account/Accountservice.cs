using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZaloClone.Data;
using ZaloClone.Infrastructure;
using ZaloClone.Models.Entities;
using ZaloClone.Services.Storage;

namespace ZaloClone.Services.Account;

/// <summary>
/// Interface nghiệp vụ cho Cặp 1 (Tài khoản &amp; Trang cá nhân).
/// Đặt tại: src/ZaloClone/Services/Account/AccountService.cs
/// </summary>
public interface IAccountService
{
    Task<ApiResponse<User>> RegisterAsync(RegisterViewModel model);
    Task<ApiResponse<User>> ValidateLoginAsync(LoginViewModel model);
    Task UpdateLastActiveAsync(Guid userId);
    Task<ApiResponse<string>> GenerateForgotPasswordOtpAsync(string emailOrPhone);
    Task<ApiResponse> ResetPasswordWithOtpAsync(ResetPasswordViewModel model);
    Task<ProfileViewModel?> GetProfileAsync(Guid userId);
    Task<ApiResponse<User>> UpdateProfileAsync(Guid userId, ProfileViewModel model);
    Task<ApiResponse<(User User, string RelativeUrl, long FileSizeBytes)>> UpdateAvatarAsync(Guid userId, IFormFile? avatarFile);
    Task<ApiResponse<User>> ChangePasswordAsync(Guid userId, ChangePasswordViewModel model);
    bool CheckPhysicalAvatarExists(string? avatarUrl, out long fileSizeBytes);
    Task SignInUserAsync(HttpContext httpContext, User user, bool isPersistent);
}

/// <summary>
/// Thực thi toàn bộ nghiệp vụ Tài khoản &amp; Trang cá nhân (Mục 6 CONVENTIONS.md: Controller mỏng, logic viết trong Services/Account/).
/// </summary>
public class AccountService : IAccountService
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IFileStorageService _fileStorageService;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AccountService> _logger;

    // Lưu OTP Quên mật khẩu tạm thời (10 phút) trong bộ nhớ để không phải sửa bảng DB
    private static readonly ConcurrentDictionary<string, (string Otp, DateTimeOffset ExpiresAt)> _otpStore = new();

    public AccountService(
        AppDbContext db,
        IPasswordHasher<User> passwordHasher,
        IFileStorageService fileStorageService,
        IWebHostEnvironment env,
        ILogger<AccountService> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _fileStorageService = fileStorageService;
        _env = env;
        _logger = logger;
    }

    // 1. ĐĂNG KÝ (BR-01 -> BR-05)
    public async Task<ApiResponse<User>> RegisterAsync(RegisterViewModel model)
    {
        var normalizedEmail = model.Email.Trim().ToLowerInvariant();

        string? normalizedPhone = null;
        if (!string.IsNullOrWhiteSpace(model.PhoneNumber))
        {
            normalizedPhone = NormalizePhone(model.PhoneNumber);
            if (normalizedPhone == null)
            {
                return ApiResponse<User>.Fail("Số điện thoại không hợp lệ (chỉ gồm 9-11 chữ số, ví dụ 0901234567).");
            }
        }

        var trimmedDisplayName = model.DisplayName.Trim();
        if (string.IsNullOrEmpty(trimmedDisplayName))
        {
            return ApiResponse<User>.Fail("Tên hiển thị không được để trống.");
        }

        var emailExists = await _db.Users.AsNoTracking()
            .AnyAsync(u => !u.IsDeleted && u.Email.ToLower() == normalizedEmail);
        if (emailExists)
        {
            return ApiResponse<User>.Fail("Email này đã được sử dụng bởi một tài khoản khác.");
        }

        if (normalizedPhone != null)
        {
            var phoneExists = await _db.Users.AsNoTracking()
                .AnyAsync(u => !u.IsDeleted && u.PhoneNumber == normalizedPhone);
            if (phoneExists)
            {
                return ApiResponse<User>.Fail("Số điện thoại này đã được đăng ký cho tài khoản khác.");
            }
        }

        var now = DateTimeOffset.UtcNow;
        var newUser = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            PhoneNumber = normalizedPhone,
            DisplayName = trimmedDisplayName,
            SecurityStamp = Guid.NewGuid(),
            Role = "User",
            IsLocked = false,
            IsDeleted = false,
            LastActiveAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        newUser.PasswordHash = _passwordHasher.HashPassword(newUser, model.Password);

        try
        {
            _db.Users.Add(newUser);
            await _db.SaveChangesAsync();
            return ApiResponse<User>.Ok(newUser, "Đăng ký tài khoản thành công!");
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Lỗi DB khi đăng ký {Email}", normalizedEmail);
            return ApiResponse<User>.Fail("Email hoặc Số điện thoại đã tồn tại trong hệ thống.");
        }
    }

    // 2. XÁC THỰC ĐĂNG NHẬP
    public async Task<ApiResponse<User>> ValidateLoginAsync(LoginViewModel model)
    {
        var rawInput = model.EmailOrPhone.Trim();
        User? user;

        if (rawInput.Contains('@'))
        {
            var normalizedEmail = rawInput.ToLowerInvariant();
            user = await _db.Users.FirstOrDefaultAsync(u => !u.IsDeleted && u.Email.ToLower() == normalizedEmail);
        }
        else
        {
            var normalizedPhone = NormalizePhone(rawInput) ?? rawInput;
            user = await _db.Users.FirstOrDefaultAsync(u => !u.IsDeleted && u.PhoneNumber != null &&
                (u.PhoneNumber == normalizedPhone || u.PhoneNumber == rawInput));
        }

        if (user == null)
        {
            return ApiResponse<User>.Fail("Tài khoản hoặc mật khẩu không chính xác.");
        }

        if (user.IsLocked)
        {
            return ApiResponse<User>.Fail("Tài khoản của bạn đã bị khóa bởi Quản trị viên.");
        }

        var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);
        if (verifyResult == PasswordVerificationResult.Failed)
        {
            return ApiResponse<User>.Fail("Tài khoản hoặc mật khẩu không chính xác.");
        }

        if (verifyResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);
        }

        user.LastActiveAt = DateTimeOffset.UtcNow;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return ApiResponse<User>.Ok(user, "Đăng nhập thành công.");
    }

    public async Task UpdateLastActiveAsync(Guid userId)
    {
        if (userId == Guid.Empty) return;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
        if (user != null)
        {
            user.LastActiveAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    // 3. QUÊN MẬT KHẨU & ĐẶT LẠI MẬT KHẨU
    public async Task<ApiResponse<string>> GenerateForgotPasswordOtpAsync(string emailOrPhone)
    {
        var rawInput = emailOrPhone.Trim();
        User? user;

        if (rawInput.Contains('@'))
        {
            var normalizedEmail = rawInput.ToLowerInvariant();
            user = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => !u.IsDeleted && u.Email.ToLower() == normalizedEmail);
        }
        else
        {
            var normalizedPhone = NormalizePhone(rawInput) ?? rawInput;
            user = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => !u.IsDeleted && u.PhoneNumber != null &&
                    (u.PhoneNumber == normalizedPhone || u.PhoneNumber == rawInput));
        }

        if (user == null)
        {
            return ApiResponse<string>.Fail("Không tìm thấy tài khoản hoạt động nào khớp với thông tin vừa nhập.");
        }

        var otpCode = Random.Shared.Next(100000, 999999).ToString();
        _otpStore[user.Email.ToLowerInvariant()] = (otpCode, DateTimeOffset.UtcNow.AddMinutes(10));
        _logger.LogInformation("[ZaloClone OTP] Mã đặt lại mật khẩu cho {Email} là: {OtpCode}", user.Email, otpCode);

        return ApiResponse<string>.Ok(user.Email, $"[Chế độ Test Đồ Án] Mã OTP của tài khoản {user.Email} là: {otpCode} (Hiệu lực 10 phút).");
    }

    public async Task<ApiResponse> ResetPasswordWithOtpAsync(ResetPasswordViewModel model)
    {
        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        if (!_otpStore.TryGetValue(normalizedEmail, out var otpEntry) ||
            otpEntry.ExpiresAt < DateTimeOffset.UtcNow ||
            otpEntry.Otp != model.OtpCode.Trim())
        {
            return ApiResponse.Fail("Mã OTP không chính xác hoặc đã hết hạn.");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => !u.IsDeleted && u.Email.ToLower() == normalizedEmail);
        if (user == null)
        {
            return ApiResponse.Fail("Tài khoản không tồn tại.");
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, model.NewPassword);
        user.SecurityStamp = Guid.NewGuid();
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        _otpStore.TryRemove(normalizedEmail, out _);
        return ApiResponse.Ok("Đặt lại mật khẩu thành công! Vui lòng đăng nhập bằng mật khẩu mới.");
    }

    // 4. TRANG CÁ NHÂN (XEM, SỬA, AVATAR, ĐỔI MẬT KHẨU)
    public async Task<ProfileViewModel?> GetProfileAsync(Guid userId)
    {
        if (userId == Guid.Empty) return null;

        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
        if (user == null) return null;

        var exists = CheckPhysicalAvatarExists(user.AvatarUrl, out var sizeBytes);

        return new ProfileViewModel
        {
            Id = user.Id,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl,
            AvatarFileExistsOnDisk = exists,
            AvatarFileSizeBytes = sizeBytes,
            Bio = user.Bio,
            Role = user.Role,
            CreatedAt = user.CreatedAt,
            LastActiveAt = user.LastActiveAt
        };
    }

    public async Task<ApiResponse<User>> UpdateProfileAsync(Guid userId, ProfileViewModel model)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
        if (user == null)
        {
            return ApiResponse<User>.Fail("Không tìm thấy tài khoản.");
        }

        var trimmedName = (model.DisplayName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmedName))
        {
            return ApiResponse<User>.Fail("Tên hiển thị không được để trống.");
        }

        string? normalizedPhone = null;
        if (!string.IsNullOrWhiteSpace(model.PhoneNumber))
        {
            normalizedPhone = NormalizePhone(model.PhoneNumber);
            if (normalizedPhone == null)
            {
                return ApiResponse<User>.Fail("Số điện thoại không hợp lệ (chỉ gồm 9-11 chữ số, ví dụ 0901234567).");
            }

            if (normalizedPhone != user.PhoneNumber)
            {
                var phoneExists = await _db.Users.AsNoTracking()
                    .AnyAsync(u => !u.IsDeleted && u.Id != user.Id && u.PhoneNumber == normalizedPhone);
                if (phoneExists)
                {
                    return ApiResponse<User>.Fail("Số điện thoại này đã được liên kết với một tài khoản khác.");
                }
            }
        }

        user.DisplayName = trimmedName;
        user.PhoneNumber = normalizedPhone;
        user.Bio = string.IsNullOrWhiteSpace(model.Bio) ? null : model.Bio.Trim();
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return ApiResponse<User>.Ok(user, "Cập nhật thông tin cá nhân thành công!");
    }

    public async Task<ApiResponse<(User User, string RelativeUrl, long FileSizeBytes)>> UpdateAvatarAsync(Guid userId, IFormFile? avatarFile)
    {
        if (avatarFile == null || avatarFile.Length == 0)
        {
            return ApiResponse<(User, string, long)>.Fail("Vui lòng chọn một tệp ảnh hợp lệ.");
        }

        if (!_fileStorageService.IsAllowedExtension(avatarFile.FileName, "avatars"))
        {
            return ApiResponse<(User, string, long)>.Fail("Chỉ hỗ trợ định dạng ảnh .jpg, .jpeg, .png hoặc .webp.");
        }

        if (avatarFile.Length > _fileStorageService.GetMaxSizeBytes("avatars"))
        {
            return ApiResponse<(User, string, long)>.Fail("Dung lượng ảnh đại diện không được vượt quá 5 MB.");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
        if (user == null)
        {
            return ApiResponse<(User, string, long)>.Fail("Không tìm thấy người dùng.");
        }

        var oldAvatarUrl = user.AvatarUrl;
        var storageKey = await _fileStorageService.SaveFileAsync(avatarFile, "avatars");
        var newRelativeUrl = _fileStorageService.GetRelativeUrl(storageKey);

        if (!CheckPhysicalAvatarExists(newRelativeUrl, out var savedBytes) || savedBytes <= 0)
        {
            return ApiResponse<(User, string, long)>.Fail("Lỗi lưu trữ: Không tìm thấy file ảnh sau khi ghi xuống ổ đĩa.");
        }

        if (!string.IsNullOrWhiteSpace(oldAvatarUrl) && oldAvatarUrl.StartsWith("/uploads/avatars/"))
        {
            await _fileStorageService.DeleteFileAsync(oldAvatarUrl.TrimStart('/'));
        }

        user.AvatarUrl = newRelativeUrl;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return ApiResponse<(User, string, long)>.Ok(
            (user, newRelativeUrl, savedBytes),
            $"Cập nhật ảnh đại diện thành công! Đã lưu tại {newRelativeUrl} ({savedBytes / 1024.0:F1} KB).");
    }

    public async Task<ApiResponse<User>> ChangePasswordAsync(Guid userId, ChangePasswordViewModel model)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
        if (user == null)
        {
            return ApiResponse<User>.Fail("Không tìm thấy tài khoản.");
        }

        var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.CurrentPassword);
        if (verifyResult == PasswordVerificationResult.Failed)
        {
            return ApiResponse<User>.Fail("Mật khẩu hiện tại không chính xác.");
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, model.NewPassword);
        user.SecurityStamp = Guid.NewGuid();
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return ApiResponse<User>.Ok(user, "Đổi mật khẩu thành công!");
    }

    public bool CheckPhysicalAvatarExists(string? avatarUrl, out long fileSizeBytes)
    {
        fileSizeBytes = 0;
        if (string.IsNullOrWhiteSpace(avatarUrl) || !avatarUrl.StartsWith("/uploads/"))
        {
            return false;
        }

        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var relativePath = avatarUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var physicalPath = Path.Combine(webRoot, relativePath);

        var info = new FileInfo(physicalPath);
        if (info.Exists)
        {
            fileSizeBytes = info.Length;
            return true;
        }
        return false;
    }

    public async Task SignInUserAsync(HttpContext httpContext, User user, bool isPersistent)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role ?? "User"),
            new("SecurityStamp", user.SecurityStamp.ToString()),
            new("AvatarUrl", user.AvatarUrl ?? string.Empty)
        };

        if (!string.IsNullOrEmpty(user.PhoneNumber))
        {
            claims.Add(new Claim(ClaimTypes.MobilePhone, user.PhoneNumber));
        }

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = isPersistent,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7),
            AllowRefresh = true
        };

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);
    }

    private static string? NormalizePhone(string input)
    {
        var digits = new string(input.Where(c => char.IsDigit(c) || c == '+').ToArray());
        if (digits.StartsWith("+84")) digits = "0" + digits[3..];
        else if (digits.StartsWith("84") && digits.Length >= 11) digits = "0" + digits[2..];

        if (digits.Any(c => !char.IsDigit(c))) return null;
        return digits.Length is >= 9 and <= 11 ? digits : null;
    }
}

// =========================================================================
// VIEW MODELS CỦA CẶP 1 (Đặt trong Services/Account/AccountService.cs để dùng chung
// cho cả AccountController và ProfileController mà không đụng thư mục Models/)
// =========================================================================

public class RegisterViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập Email.")]
    [EmailAddress(ErrorMessage = "Định dạng Email không hợp lệ.")]
    [StringLength(254, ErrorMessage = "Email tối đa 254 ký tự.")]
    public string Email { get; set; } = string.Empty;

    [StringLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự.")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên hiển thị.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Tên hiển thị từ 2 đến 100 ký tự.")]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class LoginViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập Số điện thoại hoặc Email.")]
    public string EmailOrPhone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; } = true;

    public string? ReturnUrl { get; set; }
}

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập Email hoặc Số điện thoại đã đăng ký.")]
    public string EmailOrPhone { get; set; } = string.Empty;
}

public class ResetPasswordViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập Email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mã xác nhận (OTP).")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã OTP gồm đúng 6 chữ số.")]
    public string OtpCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu mới phải từ 6 ký tự trở lên.")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu mới.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại.")]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu mới phải có ít nhất 6 ký tự.")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu mới.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

public class ProfileViewModel
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;

    [StringLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự.")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Tên hiển thị không được để trống.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Tên hiển thị từ 2 đến 100 ký tự.")]
    public string DisplayName { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }
    public bool AvatarFileExistsOnDisk { get; set; }
    public long AvatarFileSizeBytes { get; set; }

    [StringLength(255, ErrorMessage = "Tiểu sử tối đa 255 ký tự.")]
    public string? Bio { get; set; }

    public string Role { get; set; } = "User";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastActiveAt { get; set; }
}
