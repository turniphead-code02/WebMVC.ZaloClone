namespace ZaloClone.Services.Storage;

public class LocalFileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<LocalFileStorageService> _logger;

    private static readonly Dictionary<string, string[]> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["avatars"] = [".jpg", ".jpeg", ".png", ".webp"],
        ["groups"] = [".jpg", ".jpeg", ".png", ".webp"],
        ["chat-images"] = [".jpg", ".jpeg", ".png", ".gif", ".webp"],
        ["chat-files"] = [".pdf", ".docx", ".xlsx", ".pptx", ".zip", ".txt"]
    };

    private static readonly Dictionary<string, long> MaxFileSizes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["avatars"] = 5 * 1024 * 1024,      // 5 MB
        ["groups"] = 5 * 1024 * 1024,       // 5 MB
        ["chat-images"] = 10 * 1024 * 1024, // 10 MB
        ["chat-files"] = 50 * 1024 * 1024   // 50 MB
    };

    public LocalFileStorageService(IWebHostEnvironment environment, ILogger<LocalFileStorageService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public async Task<string> SaveFileAsync(IFormFile file, string category, CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("Tệp tải lên không được để trống.", nameof(file));
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!IsAllowedExtension(file.FileName, category))
        {
            throw new InvalidOperationException($"Định dạng tệp '{ext}' không được hỗ trợ cho mục {category}.");
        }

        if (file.Length > GetMaxSizeBytes(category))
        {
            throw new InvalidOperationException($"Kích thước tệp vượt quá giới hạn tối đa cho phép.");
        }

        var now = DateTime.UtcNow;
        bool isPrivateChatFile = category.Equals("chat-images", StringComparison.OrdinalIgnoreCase)
                              || category.Equals("chat-files", StringComparison.OrdinalIgnoreCase);

        var uniqueFileName = $"{Guid.NewGuid():N}{ext}";
        string storageKey;
        string physicalDir;

        if (isPrivateChatFile)
        {
            var relativeDir = Path.Combine("App_Data", "uploads", category, now.ToString("yyyy"), now.ToString("MM"));
            physicalDir = Path.Combine(_environment.ContentRootPath, relativeDir);
            storageKey = $"private/{category}/{now:yyyy}/{now:MM}/{uniqueFileName}";
        }
        else
        {
            var relativeDir = Path.Combine("uploads", category, now.ToString("yyyy"), now.ToString("MM"));
            physicalDir = Path.Combine(_environment.WebRootPath, relativeDir);
            storageKey = $"uploads/{category}/{now:yyyy}/{now:MM}/{uniqueFileName}";
        }

        if (!Directory.Exists(physicalDir))
        {
            Directory.CreateDirectory(physicalDir);
        }

        var physicalPath = Path.Combine(physicalDir, uniqueFileName);

        await using var stream = new FileStream(physicalPath, FileMode.Create);
        await file.CopyToAsync(stream, cancellationToken);

        _logger.LogInformation("Saved file {FileName} to {StorageKey}", file.FileName, storageKey);
        return storageKey;
    }

    public string GetPhysicalPath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return string.Empty;
        }

        var cleanKey = storageKey.Replace('\\', '/').TrimStart('/');
        if (cleanKey.StartsWith("private/", StringComparison.OrdinalIgnoreCase))
        {
            var subPath = cleanKey["private/".Length..].Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(_environment.ContentRootPath, "App_Data", "uploads", subPath);
        }

        var normalizedKey = cleanKey.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(_environment.WebRootPath, normalizedKey);
    }

    public Task DeleteFileAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return Task.CompletedTask;
        }

        var physicalPath = GetPhysicalPath(storageKey);

        if (!string.IsNullOrEmpty(physicalPath) && File.Exists(physicalPath))
        {
            File.Delete(physicalPath);
            _logger.LogInformation("Deleted file at {PhysicalPath}", physicalPath);
        }

        return Task.CompletedTask;
    }

    public string GetRelativeUrl(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return string.Empty;
        }

        return storageKey.StartsWith('/') ? storageKey : $"/{storageKey}";
    }

    public bool IsAllowedExtension(string fileName, string category)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (AllowedExtensions.TryGetValue(category, out var allowed))
        {
            return allowed.Contains(ext);
        }

        return false;
    }

    public long GetMaxSizeBytes(string category)
    {
        if (MaxFileSizes.TryGetValue(category, out var limit))
        {
            return limit;
        }

        return 10 * 1024 * 1024; // Mặc định 10MB
    }
}

