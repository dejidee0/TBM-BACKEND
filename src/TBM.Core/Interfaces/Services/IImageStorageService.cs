using System.IO;

namespace TBM.Core.Interfaces.Services
{
public interface IImageStorageService
{
    Task<string> UploadRoomImageAsync(Stream stream, string fileName, string userId);
    Task<string> UploadGeneratedMediaAsync(Stream stream, string fileName, string userId, string? contentType = null);
    Task<string> UploadDocumentAsync(Stream stream, string fileName, string userId, string? contentType = null);
    Task<string> UploadGalleryImageAsync(Stream stream, string fileName, string userId, string? contentType = null);
}
}
