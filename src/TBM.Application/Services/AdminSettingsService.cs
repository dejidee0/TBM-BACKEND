using TBM.Application.Interfaces;
using TBM.Application.DTOs.Settings;
using TBM.Core.Entities;
using TBM.Core.Interfaces;

namespace TBM.Application.Services;

public class AdminSettingsService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISettingsManager _settingsManager;
    private readonly AuditService _audit;

    public AdminSettingsService(
        IUnitOfWork unitOfWork,
        AuditService audit,
        ISettingsManager settingsManager)
    {
        _unitOfWork = unitOfWork;
        _settingsManager = settingsManager;
        _audit = audit;
    }

    public async Task<T?> GetCategoryAsync<T>(string category) where T : class
    {
        return await _settingsManager.GetAsync<T>(category);
    }

    public async Task SaveCategoryAsync<T>(string category, T dto) where T : class
    {
        var oldValue = await _settingsManager.GetAsync<T>(category);

        await _settingsManager.SaveAsync(category, dto);

        await _audit.LogAsync(
            action: "SettingsUpdated",
            category: category,
            oldValue: oldValue,
            newValue: dto);
    }

    public async Task<AdminNotificationSettingsDto> GetNotificationsAsync()
    {
        var result = await _settingsManager.GetAsync<AdminNotificationSettingsDto>("AdminNotifications");
        return result ?? new AdminNotificationSettingsDto();
    }

    public async Task SaveNotificationsAsync(AdminNotificationSettingsDto dto)
    {
        await SaveCategoryAsync("AdminNotifications", dto);
    }

    public async Task<PaymentGatewayDto> PatchPaymentGatewayAsync(string gatewayId, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(gatewayId))
        {
            throw new ArgumentException("Gateway ID is required.", nameof(gatewayId));
        }

        var normalizedId = gatewayId.Trim().ToLowerInvariant();
        var previousValue = await GetToggleAsync("PaymentGatewayToggles", normalizedId);

        await SaveToggleAsync("PaymentGatewayToggles", normalizedId, enabled);

        var gateway = new PaymentGatewayDto
        {
            Id = normalizedId,
            Enabled = enabled,
            PublicKey = string.Empty,
            SecretKey = string.Empty
        };

        await _audit.LogAsync(
            action: "SettingsUpdated",
            category: "PaymentGatewayToggles",
            oldValue: new { id = normalizedId, enabled = previousValue },
            newValue: gateway);

        return gateway;
    }

    public async Task<AIModelDto> PatchAIModelAsync(string modelId, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new ArgumentException("Model ID is required.", nameof(modelId));
        }

        var normalizedId = modelId.Trim().ToLowerInvariant();
        var previousValue = await GetToggleAsync("AIModelToggles", normalizedId);

        await SaveToggleAsync("AIModelToggles", normalizedId, enabled);

        var model = new AIModelDto
        {
            Id = normalizedId,
            Enabled = enabled,
            ApiKey = string.Empty,
            MaxTokens = 0
        };

        await _audit.LogAsync(
            action: "SettingsUpdated",
            category: "AIModelToggles",
            oldValue: new { id = normalizedId, enabled = previousValue },
            newValue: model);

        return model;
    }

    public async Task SaveLegacyPaymentFormAsync(decimal baseFee, decimal fixedFee, string? currency)
    {
        var existing = await GetCategoryAsync<PaymentSettingsDto>("Payment")
            ?? new PaymentSettingsDto();

        existing.BasePlatformFee = baseFee;
        existing.FixedFeePerTransaction = fixedFee;

        if (!string.IsNullOrWhiteSpace(currency))
        {
            existing.DefaultCurrency = currency.Trim().ToUpperInvariant();
        }

        await SaveCategoryAsync("Payment", existing);
    }

    private async Task<bool?> GetToggleAsync(string category, string key)
    {
        var existing = await _unitOfWork.Settings.GetByKeyAsync(category, key);
        if (existing == null)
        {
            return null;
        }

        if (bool.TryParse(existing.Value, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private async Task SaveToggleAsync(string category, string key, bool enabled)
    {
        var existing = await _unitOfWork.Settings.GetByKeyAsync(category, key);

        if (existing == null)
        {
            await _unitOfWork.Settings.AddAsync(new Setting
            {
                Category = category,
                Key = key,
                Value = enabled.ToString(),
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.Value = enabled.ToString();
            existing.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Settings.UpdateAsync(existing);
        }

        await _unitOfWork.SaveChangesAsync();
        await _settingsManager.RefreshAsync(category);
    }
}
