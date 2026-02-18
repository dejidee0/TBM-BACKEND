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

        public Task<string> UploadRoomAsync(Stream stream, string fileName, string userId)
        {
            if (stream == null || stream.Length == 0)
                throw new ArgumentException("Invalid image stream");

            return _storage.UploadRoomImageAsync(stream, fileName, userId);
        }
    }
}
