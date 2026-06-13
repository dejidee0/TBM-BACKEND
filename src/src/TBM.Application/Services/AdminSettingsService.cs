using System.Text.Json;
using TBM.Application.Interfaces;
using TBM.Application.Services;
using TBM.Core.Entities;
using TBM.Core.Interfaces;

public class AdminSettingsService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISettingsManager _settingsManager;
private readonly AuditService _audit;

    public AdminSettingsService(IUnitOfWork unitOfWork, AuditService audit, ISettingsManager settingsManager)
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


}
