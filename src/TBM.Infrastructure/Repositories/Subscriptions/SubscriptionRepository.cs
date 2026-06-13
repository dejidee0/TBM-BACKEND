using Microsoft.EntityFrameworkCore;
using TBM.Core.Entities.Subscriptions;
using TBM.Core.Enums;
using TBM.Core.Interfaces.Repositories.Subscriptions;
using TBM.Infrastructure.Data;

namespace TBM.Infrastructure.Repositories.Subscriptions;

public class SubscriptionRepository : ISubscriptionRepository
{
    private readonly ApplicationDbContext _context;

    public SubscriptionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<Subscription?> GetActiveByUserIdAsync(Guid userId)
    {
        return _context.Subscriptions
            .Include(s => s.Discount)
            .Where(s => s.UserId == userId && s.Status == SubscriptionStatus.Active)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public Task<Subscription?> GetByIdAsync(Guid id)
    {
        return _context.Subscriptions
            .Include(s => s.Discount)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public Task<List<Subscription>> GetByUserIdAsync(Guid userId)
    {
        return _context.Subscriptions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public Task<List<Subscription>> GetExpiringBeforeAsync(DateTime cutoff)
    {
        return _context.Subscriptions
            .Where(s => s.Status == SubscriptionStatus.Active && s.EndDate <= cutoff)
            .ToListAsync();
    }

    public async Task CreateAsync(Subscription subscription)
    {
        await _context.Subscriptions.AddAsync(subscription);
    }

    public Task UpdateAsync(Subscription subscription)
    {
        _context.Subscriptions.Update(subscription);
        return Task.CompletedTask;
    }
}
