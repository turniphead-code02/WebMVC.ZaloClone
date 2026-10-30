namespace ZaloClone.Services.Storage;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(IFormFile file, string category, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(string storageKey, CancellationToken cancellationToken = default);
    string GetRelativeUrl(string storageKey);
    bool IsAllowedExtension(string fileName, string category);
    long GetMaxSizeBytes(string category);
}

