using TBM.Core.Entities.Subscriptions;
using TBM.Core.Enums;

namespace TBM.Core.Interfaces.Repositories.Subscriptions;

public interface IPricingConfigRepository
{
    Task<List<PricingConfig>> GetAllActiveAsync();
    Task<PricingConfig?> GetByTierAndCycleAsync(SubscriptionTier tier, BillingCycle cycle);
    Task<List<PricingConfig>> GetByTierAsync(SubscriptionTier tier);
    Task UpdateAsync(PricingConfig config);
    Task CreateAsync(PricingConfig config);
}
