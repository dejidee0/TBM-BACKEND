using TBM.Core.Entities.Subscriptions;
using TBM.Core.Enums;

namespace TBM.Core.Interfaces.Repositories.Subscriptions;

public interface IDiscountRepository
{
    Task<Discount?> GetByCodeAsync(string code);
    Task<Discount?> GetByIdAsync(Guid id);
    Task<List<Discount>> GetAllActiveAsync();
    Task<List<Discount>> GetExpiredAsync(DateTime cutoff);
    Task CreateAsync(Discount discount);
    Task UpdateAsync(Discount discount);
    Task DeleteAsync(Guid id);
}
