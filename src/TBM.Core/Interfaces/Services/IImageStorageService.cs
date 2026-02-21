using System.IO;

namespace TBM.Core.Interfaces.Services
{
    public interface IImageStorageService
    {
        Task<string> UploadRoomImageAsync(Stream stream, string fileName, string userId);
        Task<string> UploadGeneratedMediaAsync(Stream stream, string fileName, string userId, string? contentType = null);
    }
}
