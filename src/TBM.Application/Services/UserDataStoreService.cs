using System.Text.Json;
using TBM.Core.Entities;
using TBM.Core.Interfaces;

namespace TBM.Application.Services;

public class UserDataStoreService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IUnitOfWork _unitOfWork;

    public UserDataStoreService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<T> GetAsync<T>(string category, string key, T fallback)
    {
        var setting = await _unitOfWork.Settings.GetByKeyAsync(category, key);
        if (setting == null || string.IsNullOrWhiteSpace(setting.Value))
        {
            return fallback;
        }

        try
        {
            var value = JsonSerializer.Deserialize<T>(setting.Value, JsonOptions);
            return value ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    public async Task SaveAsync<T>(string category, string key, T value, string? description = null)
    {
        var serialized = JsonSerializer.Serialize(value, JsonOptions);
        var setting = await _unitOfWork.Settings.GetByKeyAsync(category, key);

        if (setting == null)
        {
            setting = new Setting
            {
                Category = category,
                Key = key,
                Value = serialized,
                Description = description,
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Settings.AddAsync(setting);
        }
        else
        {
            setting.Value = serialized;
            setting.Description = description ?? setting.Description;
            setting.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Settings.UpdateAsync(setting);
        }

        await _unitOfWork.SaveChangesAsync();
    }

    public static string BuildUserKey(string rootKey, Guid userId)
    {
        return $"{rootKey}:{userId:N}";
    }
}
