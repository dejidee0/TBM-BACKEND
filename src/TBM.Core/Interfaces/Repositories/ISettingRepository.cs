using TBM.Core.Entities;

namespace TBM.Core.Interfaces.Repositories;

public interface ISettingRepository
{
    Task<Setting?> GetByKeyAsync(string category, string key);
    Task<IEnumerable<Setting>> GetByCategoryAsync(string category);
    Task<IEnumerable<Setting>> GetAllAsync();
    Task AddAsync(Setting setting);
    Task UpdateAsync(Setting setting);
}
