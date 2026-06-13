using System.IO;
using TBM.Core.Interfaces.Services;

namespace TBM.Application.Services
{
    public class ImageUploadService
    {
        private readonly IImageStorageService _storage;

        public ImageUploadService(IImageStorageService storage)
        {
            _storage = storage;
        }

        private const long MaxRoomImageSizeBytes = 10 * 1024 * 1024; // 10 MB

        public Task<string> UploadRoomAsync(Stream stream, string fileName, string userId)
        {
            if (stream == null || stream.Length == 0)
                throw new ArgumentException("A valid image file is required.");

            if (stream.Length > MaxRoomImageSizeBytes)
                throw new ArgumentException(
                    $"Image file is too large ({stream.Length / 1024 / 1024.0:F1} MB). " +
                    $"Maximum allowed size is {MaxRoomImageSizeBytes / 1024 / 1024} MB. " +
                    "Please compress the image or use a lower resolution before uploading.");

            return _storage.UploadRoomImageAsync(stream, fileName, userId);
        }
    }
}
