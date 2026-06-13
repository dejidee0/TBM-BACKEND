namespace TBM.Application.Interfaces;

public interface ISettingsManager
{
    Task<T?> GetAsync<T>(string category) where T : class;
    Task SaveAsync<T>(string category, T value) where T : class;
    Task RefreshAsync(string category);
}
