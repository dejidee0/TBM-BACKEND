using Microsoft.EntityFrameworkCore;
using TBM.Core.Entities.Subscriptions;
using TBM.Core.Interfaces.Repositories.Subscriptions;
using TBM.Infrastructure.Data;

namespace TBM.Infrastructure.Repositories.Subscriptions;

public class DiscountRepository : IDiscountRepository
{
    private readonly ApplicationDbContext _context;

    public DiscountRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<Discount?> GetByCodeAsync(string code)
    {
        return _context.Discounts
            .FirstOrDefaultAsync(d => d.Code == code.ToUpperInvariant() && d.IsActive);
    }

    public Task<Discount?> GetByIdAsync(Guid id)
    {
        return _context.Discounts.FindAsync(id).AsTask();
    }

    public Task<List<Discount>> GetAllActiveAsync()
    {
        return _context.Discounts
            .Where(d => d.IsActive)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();
    }

    public Task<List<Discount>> GetExpiredAsync(DateTime cutoff)
    {
        return _context.Discounts
            .Where(d => d.IsActive && d.ExpiresAt <= cutoff)
            .ToListAsync();
    }

    public async Task CreateAsync(Discount discount)
    {
        discount.Code = discount.Code.ToUpperInvariant();
        await _context.Discounts.AddAsync(discount);
    }

    public Task UpdateAsync(Discount discount)
    {
        _context.Discounts.Update(discount);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id)
    {
        var discount = await _context.Discounts.FindAsync(id);
        if (discount != null)
        {
            discount.IsActive = false;
            _context.Discounts.Update(discount);
        }
    }
}
