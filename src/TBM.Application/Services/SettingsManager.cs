using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using TBM.Application.Interfaces;
using TBM.Core.Interfaces;

namespace TBM.Application.Services;

public class SettingsManager : ISettingsManager
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemoryCache _cache;

    public SettingsManager(IUnitOfWork unitOfWork, IMemoryCache cache)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    private string CacheKey(string category) => $"settings_{category}";

    public async Task<T?> GetAsync<T>(string category) where T : class
    {
        if (_cache.TryGetValue(CacheKey(category), out T? cached))
            return cached;

        var settings = await _unitOfWork.Settings.GetByCategoryAsync(category);

        if (!settings.Any())
            return null;

        var dict = settings.ToDictionary(x => x.Key, x => x.Value);

        var json = JsonSerializer.Serialize(dict);

        var result = JsonSerializer.Deserialize<T>(json);

        if (result != null)
        {
            _cache.Set(CacheKey(category), result, TimeSpan.FromMinutes(30));
        }

        return result;
    }

    public async Task SaveAsync<T>(string category, T value) where T : class
    {
        var json = JsonSerializer.Serialize(value);
        var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        if (dict == null)
            return;

        foreach (var item in dict)
        {
            var existing = await _unitOfWork.Settings.GetByKeyAsync(category, item.Key);

            if (existing == null)
            {
                await _unitOfWork.Settings.AddAsync(new TBM.Core.Entities.Setting
                {
                    Category = category,
                    Key = item.Key,
                    Value = item.Value?.ToString() ?? string.Empty,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.Value = item.Value?.ToString() ?? string.Empty;
                existing.UpdatedAt = DateTime.UtcNow;
                await _unitOfWork.Settings.UpdateAsync(existing);
            }
        }

        await _unitOfWork.SaveChangesAsync();

        await RefreshAsync(category);
    }

    public async Task RefreshAsync(string category)
    {
        _cache.Remove(CacheKey(category));
        await GetAsync<object>(category);
    }
}
