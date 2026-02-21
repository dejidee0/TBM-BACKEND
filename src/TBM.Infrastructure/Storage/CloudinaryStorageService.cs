using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using TBM.Core.Interfaces.Services;
using TBM.Infrastructure.Configuration;

namespace TBM.Infrastructure.Storage
{
    public class CloudinaryStorageService : IImageStorageService
    {
        private readonly Cloudinary _cloudinary;
        private readonly CloudinarySettings _settings;

        public CloudinaryStorageService(IOptions<CloudinarySettings> options)
        {
            _settings = options.Value;

            var account = new Account(
                _settings.CloudName,
                _settings.ApiKey,
                _settings.ApiSecret
            );

            _cloudinary = new Cloudinary(account);
        }

        public async Task<string> UploadRoomImageAsync(Stream stream, string fileName, string userId)
        {
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(fileName, stream),
                Folder = $"{_settings.RoomFolder}/{userId}",
                UseFilename = true,
                UniqueFilename = true,
                Overwrite = false
            };

            var result = await _cloudinary.UploadAsync(uploadParams);

            if (result.Error != null)
                throw new Exception(result.Error.Message);

            return result.SecureUrl.ToString();
        }

        public async Task<string> UploadGeneratedMediaAsync(Stream stream, string fileName, string userId, string? contentType = null)
        {
            var folder = $"{_settings.GeneratedFolder}/{userId}";

            if (IsVideoContent(fileName, contentType))
            {
                var videoUpload = new VideoUploadParams
                {
                    File = new FileDescription(fileName, stream),
                    Folder = folder,
                    UseFilename = true,
                    UniqueFilename = true,
                    Overwrite = false
                };

                var videoResult = await _cloudinary.UploadAsync(videoUpload);
                if (videoResult.Error != null)
                {
                    throw new Exception(videoResult.Error.Message);
                }

                return videoResult.SecureUrl.ToString();
            }

            var imageUpload = new ImageUploadParams
            {
                File = new FileDescription(fileName, stream),
                Folder = folder,
                UseFilename = true,
                UniqueFilename = true,
                Overwrite = false
            };

            var imageResult = await _cloudinary.UploadAsync(imageUpload);
            if (imageResult.Error != null)
            {
                throw new Exception(imageResult.Error.Message);
            }

            return imageResult.SecureUrl.ToString();
        }

        private static bool IsVideoContent(string fileName, string? contentType)
        {
            if (!string.IsNullOrWhiteSpace(contentType) &&
                contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var extension = Path.GetExtension(fileName);
            return extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".mov", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".webm", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".avi", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase);
        }
    }
}
