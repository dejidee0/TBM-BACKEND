using TBM.Core.Entities.Subscriptions;
using TBM.Core.Enums;

namespace TBM.Core.Interfaces.Repositories.Subscriptions;

public interface ISubscriptionRepository
{
    Task<Subscription?> GetActiveByUserIdAsync(Guid userId);
    Task<Subscription?> GetByIdAsync(Guid id);
    Task<List<Subscription>> GetByUserIdAsync(Guid userId);
    Task<List<Subscription>> GetExpiringBeforeAsync(DateTime cutoff);
    Task CreateAsync(Subscription subscription);
    Task UpdateAsync(Subscription subscription);
}
