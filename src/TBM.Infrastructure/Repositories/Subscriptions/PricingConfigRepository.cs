using Microsoft.EntityFrameworkCore;
using TBM.Core.Entities.Subscriptions;
using TBM.Core.Enums;
using TBM.Core.Interfaces.Repositories.Subscriptions;
using TBM.Infrastructure.Data;

namespace TBM.Infrastructure.Repositories.Subscriptions;

public class PricingConfigRepository : IPricingConfigRepository
{
    private readonly ApplicationDbContext _context;

    public PricingConfigRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<List<PricingConfig>> GetAllActiveAsync()
    {
        return _context.PricingConfigs
            .Where(p => p.IsActive)
            .OrderBy(p => p.Tier)
            .ThenBy(p => p.BillingCycle)
            .ToListAsync();
    }

    public Task<PricingConfig?> GetByTierAndCycleAsync(SubscriptionTier tier, BillingCycle cycle)
    {
        return _context.PricingConfigs
            .FirstOrDefaultAsync(p => p.Tier == tier && p.BillingCycle == cycle && p.IsActive);
    }

    public Task<List<PricingConfig>> GetByTierAsync(SubscriptionTier tier)
    {
        return _context.PricingConfigs
            .Where(p => p.Tier == tier && p.IsActive)
            .ToListAsync();
    }

    public Task UpdateAsync(PricingConfig config)
    {
        _context.PricingConfigs.Update(config);
        return Task.CompletedTask;
    }

    public async Task CreateAsync(PricingConfig config)
    {
        await _context.PricingConfigs.AddAsync(config);
    }
}
