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
                Overwrite = false,
                // Convert HEIC/HEIF to JPEG so all browsers can display it
                Format = IsHeicFile(fileName) ? "jpg" : null
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

        public async Task<string> UploadDocumentAsync(Stream stream, string fileName, string userId, string? contentType = null)
        {
            var uploadParams = new RawUploadParams
            {
                File = new FileDescription(fileName, stream),
                Folder = $"{_settings.DocumentsFolder}/{userId}",
                UseFilename = true,
                UniqueFilename = true,
                Overwrite = false
            };

            var result = await _cloudinary.UploadAsync(uploadParams);

            if (result.Error != null)
            {
                throw new Exception(result.Error.Message);
            }

            return result.SecureUrl.ToString();
        }

        public async Task<string> UploadGalleryImageAsync(Stream stream, string fileName, string userId, string? contentType = null)
        {
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(fileName, stream),
                Folder = $"{_settings.GalleryFolder}/{userId}",
                UseFilename = true,
                UniqueFilename = true,
                Overwrite = false,
                // Convert HEIC/HEIF to JPEG so all browsers can display it
                Format = IsHeicFile(fileName, contentType) ? "jpg" : null
            };

            var result = await _cloudinary.UploadAsync(uploadParams);

            if (result.Error != null)
            {
                throw new Exception(result.Error.Message);
            }

            return result.SecureUrl.ToString();
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

        /// <summary>
        /// Returns true for HEIC/HEIF files (common iPhone format).
        /// Cloudinary accepts them but browsers other than Safari cannot display HEIC,
        /// so we request conversion to JPEG on upload.
        /// </summary>
        private static bool IsHeicFile(string fileName, string? contentType = null)
        {
            if (!string.IsNullOrWhiteSpace(contentType))
            {
                var ct = contentType.ToLowerInvariant();
                if (ct.Contains("heic") || ct.Contains("heif"))
                    return true;
            }

            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext is ".heic" or ".heif";
        }

    }
}
