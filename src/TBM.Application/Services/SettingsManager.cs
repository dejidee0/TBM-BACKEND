using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using TBM.Application.Interfaces;
using TBM.Core.Interfaces;

namespace TBM.Application.Services;

public class SettingsManager : ISettingsManager
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemoryCache _cache;
    private static readonly JsonSerializerOptions DeserializationOptions = CreateDeserializationOptions();

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

        var latestPerKey = settings
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => g
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .First());

        var json = BuildJsonObject(latestPerKey);
        var result = JsonSerializer.Deserialize<T>(json, DeserializationOptions);

        if (result != null)
        {
            _cache.Set(CacheKey(category), result, TimeSpan.FromMinutes(30));
        }

        return result;
    }

    public async Task SaveAsync<T>(string category, T value) where T : class
    {
        var root = JsonSerializer.SerializeToElement(value);
        if (root.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in root.EnumerateObject())
        {
            var existing = await _unitOfWork.Settings.GetByKeyAsync(category, property.Name);
            var rawValue = property.Value.GetRawText();

            if (existing == null)
            {
                await _unitOfWork.Settings.AddAsync(new TBM.Core.Entities.Setting
                {
                    Category = category,
                    Key = property.Name,
                    Value = rawValue,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.Value = rawValue;
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

    private static string BuildJsonObject(IEnumerable<TBM.Core.Entities.Setting> settings)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        foreach (var setting in settings)
        {
            writer.WritePropertyName(setting.Key);
            WriteStoredValue(writer, setting.Value);
        }

        writer.WriteEndObject();
        writer.Flush();

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteStoredValue(Utf8JsonWriter writer, string? rawValue)
    {
        if (rawValue == null)
        {
            writer.WriteNullValue();
            return;
        }

        if (TryWriteParsedJson(writer, rawValue))
        {
            return;
        }

        if (bool.TryParse(rawValue, out var boolValue))
        {
            writer.WriteBooleanValue(boolValue);
            return;
        }

        if (long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
        {
            writer.WriteNumberValue(longValue);
            return;
        }

        if (decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
        {
            writer.WriteNumberValue(decimalValue);
            return;
        }

        writer.WriteStringValue(rawValue);
    }

    private static bool TryWriteParsedJson(Utf8JsonWriter writer, string rawValue)
    {
        try
        {
            using var document = JsonDocument.Parse(rawValue);
            document.RootElement.WriteTo(writer);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static JsonSerializerOptions CreateDeserializationOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        options.Converters.Add(new LenientBooleanJsonConverter());
        return options;
    }

    private sealed class LenientBooleanJsonConverter : JsonConverter<bool>
    {
        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.True)
            {
                return true;
            }

            if (reader.TokenType == JsonTokenType.False)
            {
                return false;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var text = reader.GetString();

                if (bool.TryParse(text, out var boolValue))
                {
                    return boolValue;
                }

                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                {
                    return intValue != 0;
                }
            }

            if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var numberValue))
            {
                return numberValue != 0;
            }

            throw new JsonException("Expected boolean value.");
        }

        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
        {
            writer.WriteBooleanValue(value);
        }
    }
}
