using TBM.Application.DTOs.AI;
using TBM.Core.Enums;
using TBM.Core.Interfaces.Services;

namespace TBM.Application.Services;

public class AIGeneratedMediaService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IImageStorageService _imageStorageService;

    public AIGeneratedMediaService(
        IHttpClientFactory httpClientFactory,
        IImageStorageService imageStorageService)
    {
        _httpClientFactory = httpClientFactory;
        _imageStorageService = imageStorageService;
    }

    public async Task<GeneratedMediaPersistenceResultDto> PersistAsync(
        Guid userId,
        string temporaryProviderUrl,
        AIOutputType outputType)
    {
        if (string.IsNullOrWhiteSpace(temporaryProviderUrl))
        {
            throw new InvalidOperationException("Provider returned an empty output URL.");
        }

        var client = _httpClientFactory.CreateClient();
        using var response = await client.GetAsync(
            temporaryProviderUrl,
            HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var contentType = response.Content.Headers.ContentType?.MediaType;
        await using var providerStream = await response.Content.ReadAsStreamAsync();

        var fileName = BuildGeneratedFileName(temporaryProviderUrl, outputType, contentType);
        var cloudinaryUrl = await _imageStorageService.UploadGeneratedMediaAsync(
            providerStream,
            fileName,
            userId.ToString("N"),
            contentType);

        return new GeneratedMediaPersistenceResultDto
        {
            CloudinaryUrl = cloudinaryUrl,
            TemporaryProviderUrl = temporaryProviderUrl,
            FileName = fileName,
            ContentType = contentType
        };
    }

    private static string BuildGeneratedFileName(string sourceUrl, AIOutputType outputType, string? contentType)
    {
        var extension = ResolveExtension(sourceUrl, outputType, contentType);
        return $"ai-generated-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}{extension}";
    }

    private static string ResolveExtension(string sourceUrl, AIOutputType outputType, string? contentType)
    {
        if (Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri))
        {
            var uriExt = Path.GetExtension(uri.AbsolutePath);
            if (!string.IsNullOrWhiteSpace(uriExt))
            {
                return uriExt;
            }
        }

        if (!string.IsNullOrWhiteSpace(contentType))
        {
            if (contentType.Contains("png", StringComparison.OrdinalIgnoreCase)) return ".png";
            if (contentType.Contains("jpeg", StringComparison.OrdinalIgnoreCase)) return ".jpg";
            if (contentType.Contains("webp", StringComparison.OrdinalIgnoreCase)) return ".webp";
            if (contentType.Contains("gif", StringComparison.OrdinalIgnoreCase)) return ".gif";
            if (contentType.Contains("mp4", StringComparison.OrdinalIgnoreCase)) return ".mp4";
            if (contentType.Contains("quicktime", StringComparison.OrdinalIgnoreCase)) return ".mov";
            if (contentType.Contains("webm", StringComparison.OrdinalIgnoreCase)) return ".webm";
        }

        return outputType == AIOutputType.Video ? ".mp4" : ".png";
    }
}
