using TBM.Core.Interfaces.Services;

namespace TBM.Application.Services;

public class DocumentUploadService
{
    private const long MaxDocumentSizeBytes = 50L * 1024 * 1024; // 50 MB

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".heic", ".heif", ".pdf", ".mp4", ".mov"
    };

    private readonly IImageStorageService _storage;

    public DocumentUploadService(IImageStorageService storage)
    {
        _storage = storage;
    }

    public Task<string> UploadDocumentAsync(Stream stream, string fileName, string? contentType)
    {
        if (stream == null || stream.Length == 0)
        {
            throw new ArgumentException("A file is required.");
        }

        if (stream.Length > MaxDocumentSizeBytes)
        {
            throw new ArgumentException(
                $"File is too large ({stream.Length / 1024 / 1024.0:F1} MB). Maximum allowed size is 50 MB.");
        }

        if (!IsAllowedFile(fileName, contentType))
        {
            throw new ArgumentException("Unsupported file type. Accepted types: images, PDF, MP4, MOV.");
        }

        return _storage.UploadDocumentAsync(stream, fileName, "anonymous", contentType);
    }

    private static bool IsAllowedFile(string fileName, string? contentType)
    {
        var extension = Path.GetExtension(fileName);
        if (!string.IsNullOrEmpty(extension) && AllowedExtensions.Contains(extension))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        return contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            || contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
            || contentType.Equals("video/mp4", StringComparison.OrdinalIgnoreCase)
            || contentType.Equals("video/quicktime", StringComparison.OrdinalIgnoreCase);
    }
}
