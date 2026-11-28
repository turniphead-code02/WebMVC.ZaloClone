#pragma warning disable IDE1006, IDE0290, IDE0305, IDE0028, IDE0031, IDE0057, CA1853, CA1862
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ZaloClone.Data;
using ZaloClone.Infrastructure;
using ZaloClone.Models.Entities;
using ZaloClone.Services.Storage;

namespace ZaloClone.Services.Account;

public interface IAccountService
{
    Task<ApiResponse<User>> RegisterAsync(RegisterViewModel model);
    Task<ApiResponse<User>> ValidateLoginAsync(LoginViewModel model);
    Task UpdateLastActiveAsync(Guid userId);
    Task<ApiResponse<string>> GenerateForgotPasswordOtpAsync(string emailOrPhone);
    Task<ApiResponse> ResetPasswordWithOtpAsync(ResetPasswordViewModel model);
    Task<ProfileViewModel?> GetProfileAsync(Guid targetUserId, Guid currentUserId);
    Task<ApiResponse<User>> UpdateProfileAsync(Guid userId, ProfileViewModel model);
    Task<ApiResponse<User>> UpdateAvatarAsync(Guid userId, IFormFile? avatarFile);
    Task<ApiResponse<User>> DeleteAvatarAsync(Guid userId);
    Task<ApiResponse<string>> UpdateCoverAsync(Guid userId, IFormFile? coverFile);
    Task<ApiResponse> DeleteCoverAsync(Guid userId);
    Task<ApiResponse<User>> ChangePasswordAsync(Guid userId, ChangePasswordViewModel model);
    Task SignInUserAsync(HttpContext httpContext, User user, bool isPersistent);
}

public class AccountService : IAccountService
{
    private sealed class OtpEntry
    {
        public string Otp { get; init; } = string.Empty;
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset ExpiresAt { get; init; }
        public int FailedAttempts { get; set; }
    }

    private const int _maxOtpAttempts = 5;
    private const int _otpCooldownSeconds = 30;
    private static readonly ConcurrentDictionary<string, OtpEntry> _otpStore = new();
    private static readonly string[] _allowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IFileStorageService _fileStorageService;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration? _configuration;

    public AccountService(
        AppDbContext db,
        IPasswordHasher<User> passwordHasher,
        IFileStorageService fileStorageService,
        IWebHostEnvironment env,
        IConfiguration? configuration = null)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _fileStorageService = fileStorageService;
        _env = env;
        _configuration = configuration;
    }

    public async Task<ApiResponse<User>> RegisterAsync(RegisterViewModel model)
    {
        await _db.Database.MigrateAsync();

        var email = model.Email.Trim().ToLowerInvariant();
        var displayName = model.DisplayName.Trim();
        if (string.IsNullOrEmpty(displayName))
        {
            return ApiResponse<User>.Fail("Tên hiển thị không được để trống.");
        }

        string? phone = null;
        if (!string.IsNullOrWhiteSpace(model.PhoneNumber))
        {
            phone = NormalizePhone(model.PhoneNumber);
            if (phone == null)
            {
                return ApiResponse<User>.Fail("Số điện thoại không hợp lệ (chỉ gồm 9-11 chữ số).");
            }
        }

        if (await _db.Users.AnyAsync(u => !u.IsDeleted && u.Email.ToLower() == email))
        {
            return ApiResponse<User>.Fail("Email này đã được sử dụng bởi tài khoản khác.");
        }

        if (phone != null && await _db.Users.AnyAsync(u => !u.IsDeleted && u.PhoneNumber == phone))
        {
            return ApiResponse<User>.Fail("Số điện thoại này đã được sử dụng bởi tài khoản khác.");
        }

        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PhoneNumber = phone,
            DisplayName = displayName,
            SecurityStamp = Guid.NewGuid(),
            Role = "User",
            LastActiveAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        if (model.DateOfBirth.HasValue)
        {
            await SaveBirthDateAsync(user.Id, model.DateOfBirth.Value);
        }

        return ApiResponse<User>.Ok(user, "Đăng ký tài khoản thành công! Chào mừng bạn đến với ZaloClone.");
    }

    public async Task<ApiResponse<User>> ValidateLoginAsync(LoginViewModel model)
    {
        await _db.Database.MigrateAsync();

        var input = model.EmailOrPhone.Trim();
        User? user;

        if (input.Contains('@'))
        {
            var email = input.ToLowerInvariant();
            user = await _db.Users.FirstOrDefaultAsync(u => !u.IsDeleted && u.Email.ToLower() == email);
        }
        else
        {
            var phone = NormalizePhone(input) ?? input;
            user = await _db.Users.FirstOrDefaultAsync(u => !u.IsDeleted && u.PhoneNumber != null &&
                (u.PhoneNumber == phone || u.PhoneNumber == input));
        }

        if (user == null)
        {
            return ApiResponse<User>.Fail("Tài khoản hoặc mật khẩu không chính xác.");
        }

        if (user.IsLocked)
        {
            return ApiResponse<User>.Fail("Tài khoản của bạn đã bị khóa bởi Quản trị viên.");
        }

        var check = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);
        if (check == PasswordVerificationResult.Failed)
        {
            return ApiResponse<User>.Fail("Tài khoản hoặc mật khẩu không chính xác.");
        }


        user.LastActiveAt = DateTimeOffset.UtcNow;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return ApiResponse<User>.Ok(user, $"Đăng nhập thành công! Xin chào {user.DisplayName}.");
    }

    public async Task UpdateLastActiveAsync(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return;
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
        if (user != null)
        {
            user.LastActiveAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    public async Task<ApiResponse<string>> GenerateForgotPasswordOtpAsync(string emailOrPhone)
    {
        await _db.Database.MigrateAsync();

        var input = emailOrPhone.Trim();
        User? user;

        if (input.Contains('@'))
        {
            var email = input.ToLowerInvariant();
            user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => !u.IsDeleted && u.Email.ToLower() == email);
        }
        else
        {
            var phone = NormalizePhone(input) ?? input;
            user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => !u.IsDeleted && u.PhoneNumber != null &&
                (u.PhoneNumber == phone || u.PhoneNumber == input));
        }

        if (user == null)
        {
            return ApiResponse<string>.Fail("Không tìm thấy tài khoản nào khớp với Email hoặc Số điện thoại vừa nhập.");
        }

        var emailKey = user.Email.ToLowerInvariant();
        var now = DateTimeOffset.UtcNow;
        if (_otpStore.TryGetValue(emailKey, out var existing) && existing.ExpiresAt > now)
        {
            var elapsed = (now - existing.CreatedAt).TotalSeconds;
            if (elapsed < _otpCooldownSeconds)
            {
                var remain = (int)Math.Ceiling(_otpCooldownSeconds - elapsed);
                return ApiResponse<string>.Fail($"Vui lòng chờ {remain} giây trước khi yêu cầu mã OTP mới.");
            }
        }

        var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        _otpStore[emailKey] = new OtpEntry
        {
            Otp = otp,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(10),
            FailedAttempts = 0
        };

        var smtpHost = _configuration?["Email:SmtpHost"];
        if (!string.IsNullOrWhiteSpace(smtpHost))
        {
            try
            {
                var smtpPort = int.TryParse(_configuration?["Email:SmtpPort"], out var p) ? p : 587;
                var smtpUser = _configuration?["Email:SmtpUser"] ?? string.Empty;
                var smtpPass = _configuration?["Email:SmtpPass"] ?? string.Empty;
                var fromEmail = _configuration?["Email:FromEmail"] ?? "no-reply@zaloclone.local";
                var fromName = _configuration?["Email:FromName"] ?? "ZaloClone";
                var enableSsl = !bool.TryParse(_configuration?["Email:EnableSsl"], out var ssl) || ssl;

                using var client = new System.Net.Mail.SmtpClient(smtpHost, smtpPort)
                {
                    EnableSsl = enableSsl,
                    Credentials = string.IsNullOrWhiteSpace(smtpUser)
                        ? System.Net.CredentialCache.DefaultNetworkCredentials
                        : new System.Net.NetworkCredential(smtpUser, smtpPass)
                };

                using var mail = new System.Net.Mail.MailMessage
                {
                    From = new System.Net.Mail.MailAddress(fromEmail, fromName),
                    Subject = "[ZaloClone] Mã xác thực đặt lại mật khẩu (OTP)",
                    Body = $"<p>Xin chào <strong>{System.Net.WebUtility.HtmlEncode(user.DisplayName)}</strong>,</p>"
                         + $"<p>Mã OTP đặt lại mật khẩu của bạn là: <strong style='font-size:20px;color:#0068ff;'>{otp}</strong></p>"
                         + "<p>Mã có hiệu lực trong <strong>10 phút</strong>. Nếu bạn không yêu cầu thao tác này, vui lòng bỏ qua email.</p>",
                    IsBodyHtml = true
                };
                mail.To.Add(user.Email);
                await client.SendMailAsync(mail);

                return ApiResponse<string>.Ok(user.Email, $"Mã xác thực OTP đã được gửi đến email {user.Email} (Hiệu lực 10 phút).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OTP][SMTP Error] {ex.Message}");
                if (!_env.IsDevelopment() && !_env.IsEnvironment("Testing"))
                {
                    _otpStore.TryRemove(emailKey, out _);
                    return ApiResponse<string>.Fail("Không thể gửi email OTP lúc này. Vui lòng thử lại sau.");
                }
            }
        }

        if (_env.IsDevelopment() || _env.IsEnvironment("Testing"))
        {
            Console.WriteLine($"[OTP] Email: {user.Email} | Code: {otp} | ExpiresAt: {now.AddMinutes(10):O}");
            return ApiResponse<string>.Ok(user.Email, $"Mã xác thực OTP của tài khoản {user.Email} là: {otp} (Hiệu lực 10 phút).");
        }

        _otpStore.TryRemove(emailKey, out _);
        return ApiResponse<string>.Fail("Hệ thống gửi Email chưa được cấu hình trên môi trường Production.");
    }

    public async Task<ApiResponse> ResetPasswordWithOtpAsync(ResetPasswordViewModel model)
    {
        var email = model.Email.Trim().ToLowerInvariant();
        if (!_otpStore.TryGetValue(email, out var entry))
        {
            return ApiResponse.Fail("Mã xác thực OTP không tồn tại hoặc đã hết hạn. Vui lòng yêu cầu mã mới.");
        }

        if (entry.ExpiresAt < DateTimeOffset.UtcNow)
        {
            _otpStore.TryRemove(email, out _);
            return ApiResponse.Fail("Mã xác thực OTP đã hết hạn. Vui lòng yêu cầu mã mới.");
        }

        int currentAttempts;
        bool isMatch;
        lock (entry)
        {
            if (entry.FailedAttempts >= _maxOtpAttempts)
            {
                _otpStore.TryRemove(email, out _);
                return ApiResponse.Fail($"Bạn đã nhập sai mã OTP quá {_maxOtpAttempts} lần. Mã OTP đã bị hủy, vui lòng yêu cầu mã mới.");
            }

            isMatch = string.Equals(entry.Otp, model.OtpCode.Trim(), StringComparison.Ordinal);
            if (!isMatch)
            {
                entry.FailedAttempts++;
            }

            currentAttempts = entry.FailedAttempts;
        }

        if (!isMatch)
        {
            if (currentAttempts >= _maxOtpAttempts)
            {
                _otpStore.TryRemove(email, out _);
                return ApiResponse.Fail($"Bạn đã nhập sai mã OTP quá {_maxOtpAttempts} lần. Mã OTP đã bị vô hiệu hóa, vui lòng yêu cầu mã mới.");
            }

            return ApiResponse.Fail($"Mã xác thực OTP không chính xác. Bạn còn {_maxOtpAttempts - currentAttempts} lần thử.");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => !u.IsDeleted && u.Email.ToLower() == email);
        if (user == null)
        {
            return ApiResponse.Fail("Không tìm thấy tài khoản.");
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, model.NewPassword);
        user.SecurityStamp = Guid.NewGuid();
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        _otpStore.TryRemove(email, out _);
        return ApiResponse.Ok("Đặt lại mật khẩu mới thành công! Các thiết bị khác đã được tự động đăng xuất.");
    }

    public async Task<ProfileViewModel?> GetProfileAsync(Guid targetUserId, Guid currentUserId)
    {
        if (targetUserId == Guid.Empty)
        {
            return null;
        }

        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == targetUserId && !u.IsDeleted);
        if (user == null)
        {
            return null;
        }

        return new ProfileViewModel
        {
            Id = user.Id,
            IsOwnProfile = targetUserId == currentUserId,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            DisplayName = user.DisplayName,
            DateOfBirth = await GetBirthDateAsync(user.Id),
            AvatarUrl = user.AvatarUrl,
            CoverUrl = GetCoverUrl(user.Id),
            Bio = user.Bio,
            Role = user.Role,
            CreatedAt = user.CreatedAt
        };
    }

    public async Task<ApiResponse<User>> UpdateProfileAsync(Guid userId, ProfileViewModel model)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
        if (user == null)
        {
            return ApiResponse<User>.Fail("Không tìm thấy tài khoản.");
        }

        var name = (model.DisplayName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(name))
        {
            return ApiResponse<User>.Fail("Tên hiển thị không được để trống.");
        }

        string? phone = null;
        if (!string.IsNullOrWhiteSpace(model.PhoneNumber))
        {
            phone = NormalizePhone(model.PhoneNumber);
            if (phone == null)
            {
                return ApiResponse<User>.Fail("Số điện thoại không hợp lệ (9-11 chữ số).");
            }

            if (phone != user.PhoneNumber &&
                await _db.Users.AnyAsync(u => !u.IsDeleted && u.Id != user.Id && u.PhoneNumber == phone))
            {
                return ApiResponse<User>.Fail("Số điện thoại này đã được tài khoản khác sử dụng.");
            }
        }

        user.DisplayName = name;
        user.PhoneNumber = phone;
        user.Bio = string.IsNullOrWhiteSpace(model.Bio) ? null : model.Bio.Trim();
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        if (model.DateOfBirth.HasValue)
        {
            await SaveBirthDateAsync(user.Id, model.DateOfBirth.Value);
        }

        return ApiResponse<User>.Ok(user, "Đã lưu cập nhật thông tin cá nhân!");
    }

    public async Task<ApiResponse<User>> UpdateAvatarAsync(Guid userId, IFormFile? avatarFile)
    {
        if (avatarFile == null || avatarFile.Length == 0)
        {
            return ApiResponse<User>.Fail("Vui lòng chọn ảnh đại diện.");
        }

        if (!_fileStorageService.IsAllowedExtension(avatarFile.FileName, "avatars"))
        {
            return ApiResponse<User>.Fail("Chỉ hỗ trợ định dạng ảnh .jpg, .jpeg, .png, .webp.");
        }

        if (avatarFile.Length > _fileStorageService.GetMaxSizeBytes("avatars"))
        {
            return ApiResponse<User>.Fail("Dung lượng ảnh đại diện tối đa 5 MB.");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
        if (user == null)
        {
            return ApiResponse<User>.Fail("Không tìm thấy tài khoản.");
        }

        if (!string.IsNullOrWhiteSpace(user.AvatarUrl) && user.AvatarUrl.StartsWith("/uploads/avatars/"))
        {
            await _fileStorageService.DeleteFileAsync(user.AvatarUrl.TrimStart('/'));
        }

        var key = await _fileStorageService.SaveFileAsync(avatarFile, "avatars");
        user.AvatarUrl = _fileStorageService.GetRelativeUrl(key);
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return ApiResponse<User>.Ok(user, "Cập nhật ảnh đại diện thành công!");
    }

    public async Task<ApiResponse<User>> DeleteAvatarAsync(Guid userId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
        if (user == null)
        {
            return ApiResponse<User>.Fail("Không tìm thấy tài khoản.");
        }

        if (!string.IsNullOrWhiteSpace(user.AvatarUrl) && user.AvatarUrl.StartsWith("/uploads/avatars/"))
        {
            await _fileStorageService.DeleteFileAsync(user.AvatarUrl.TrimStart('/'));
        }

        user.AvatarUrl = null;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return ApiResponse<User>.Ok(user, "Đã xóa ảnh đại diện về mặc định!");
    }

    public async Task<ApiResponse<string>> UpdateCoverAsync(Guid userId, IFormFile? coverFile)
    {
        if (coverFile == null || coverFile.Length == 0)
        {
            return ApiResponse<string>.Fail("Vui lòng chọn ảnh bìa.");
        }

        var ext = Path.GetExtension(coverFile.FileName).ToLowerInvariant();
        if (!_allowedExtensions.Contains(ext))
        {
            return ApiResponse<string>.Fail("Ảnh bìa chỉ hỗ trợ định dạng .jpg, .jpeg, .png, .webp.");
        }

        if (coverFile.Length > 5 * 1024 * 1024)
        {
            return ApiResponse<string>.Fail("Dung lượng ảnh bìa tối đa 5 MB.");
        }

        await DeleteCoverAsync(userId);

        var coverDir = GetCoverDirectory();
        var fileName = $"cover_{userId:N}{ext}";
        var fullPath = Path.Combine(coverDir, fileName);

        await using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await coverFile.CopyToAsync(stream);
        }

        var url = $"/uploads/covers/{fileName}?v={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        return ApiResponse<string>.Ok(url, "Cập nhật ảnh bìa thành công!");
    }

    public Task<ApiResponse> DeleteCoverAsync(Guid userId)
    {
        var coverDir = GetCoverDirectory();
        foreach (var ext in _allowedExtensions)
        {
            var path = Path.Combine(coverDir, $"cover_{userId:N}{ext}");
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        return Task.FromResult(ApiResponse.Ok("Đã xóa ảnh bìa!"));
    }

    public async Task<ApiResponse<User>> ChangePasswordAsync(Guid userId, ChangePasswordViewModel model)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
        if (user == null)
        {
            return ApiResponse<User>.Fail("Không tìm thấy tài khoản.");
        }

        var check = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.CurrentPassword);
        if (check == PasswordVerificationResult.Failed)
        {
            return ApiResponse<User>.Fail("Mật khẩu hiện tại không chính xác.");
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, model.NewPassword);
        user.SecurityStamp = Guid.NewGuid();
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return ApiResponse<User>.Ok(user, "Đổi mật khẩu thành công! Các trình duyệt khác đang đăng nhập cùng tài khoản đã tự động bị đăng xuất.");
    }

    public async Task SignInUserAsync(HttpContext httpContext, User user, bool isPersistent)
    {
        var role = string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase) ? "Admin" : "User";
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, role),
            new("SecurityStamp", user.SecurityStamp.ToString()),
            new("AvatarUrl", user.AvatarUrl ?? string.Empty)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var props = new AuthenticationProperties
        {
            IsPersistent = isPersistent,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7),
            AllowRefresh = true
        };

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            props);
    }

    private string GetCoverDirectory()
    {
        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var dir = Path.Combine(webRoot, "uploads", "covers");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        return dir;
    }

    private string? GetCoverUrl(Guid userId)
    {
        var dir = GetCoverDirectory();
        foreach (var ext in _allowedExtensions)
        {
            var fileName = $"cover_{userId:N}{ext}";
            var path = Path.Combine(dir, fileName);
            if (File.Exists(path))
            {
                return $"/uploads/covers/{fileName}?v={File.GetLastWriteTimeUtc(path).Ticks}";
            }
        }

        return null;
    }

    private async Task SaveBirthDateAsync(Guid userId, DateTime birthDate)
    {
        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var dir = Path.Combine(webRoot, "uploads", "profiles");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var path = Path.Combine(dir, $"dob_{userId:N}.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new { DateOfBirth = birthDate.ToString("yyyy-MM-dd") }));
    }

    private async Task<DateTime?> GetBirthDateAsync(Guid userId)
    {
        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var path = Path.Combine(webRoot, "uploads", "profiles", $"dob_{userId:N}.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(path));
            if (doc.RootElement.TryGetProperty("DateOfBirth", out var p) && DateTime.TryParse(p.GetString(), out var d))
            {
                return d;
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static string? NormalizePhone(string input)
    {
        var digits = new string(input.Where(c => char.IsDigit(c) || c == '+').ToArray());
        if (digits.StartsWith("+84"))
        {
            digits = "0" + digits[3..];
        }
        else if (digits.StartsWith("84") && digits.Length >= 11)
        {
            digits = "0" + digits[2..];
        }

        if (digits.Any(c => !char.IsDigit(c)))
        {
            return null;
        }

        return digits.Length is >= 9 and <= 11 ? digits : null;
    }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập Email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên hiển thị.")]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn ngày sinh.")]
    [DataType(DataType.Date)]
    public DateTime? DateOfBirth { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [MinLength(6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự.")]
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

    [Required(ErrorMessage = "Vui lòng nhập mã xác nhận OTP (6 chữ số).")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã OTP gồm đúng 6 chữ số.")]
    public string OtpCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
    [MinLength(6, ErrorMessage = "Mật khẩu mới tối thiểu 6 ký tự.")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu mới.")]
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
    [MinLength(6, ErrorMessage = "Mật khẩu mới tối thiểu 6 ký tự.")]
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
    public bool IsOwnProfile { get; set; } = true;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string DisplayName { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateTime? DateOfBirth { get; set; }

    public string? AvatarUrl { get; set; }
    public string? CoverUrl { get; set; }
    public string? Bio { get; set; }
    public string Role { get; set; } = "User";
    public DateTimeOffset CreatedAt { get; set; }
}
