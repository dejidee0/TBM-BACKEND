using Microsoft.EntityFrameworkCore;
using TBM.Core.Entities.AI;
using TBM.Core.Interfaces.Repositories.AI;
using TBM.Infrastructure.Data;

namespace TBM.Infrastructure.Repositories.AI;

public class AIRenovationEstimateRepository : IAIRenovationEstimateRepository
{
    private readonly ApplicationDbContext _context;

    public AIRenovationEstimateRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task CreateAsync(AIRenovationEstimate estimate)
    {
        await _context.AIRenovationEstimates.AddAsync(estimate);
    }

    public Task<AIRenovationEstimate?> GetByIdAsync(Guid estimateId, Guid userId)
    {
        return _context.AIRenovationEstimates
            .Include(x => x.LineItems.OrderBy(i => i.CreatedAt))
            .Include(x => x.SuggestedProducts.OrderBy(s => s.CreatedAt))
            .FirstOrDefaultAsync(x =>
                x.Id == estimateId &&
                x.UserId == userId &&
                !x.IsDeleted);
    }

    public Task<List<AIRenovationEstimate>> GetByUserAsync(Guid userId, int take = 100)
    {
        var safeTake = take < 1 ? 100 : Math.Min(take, 200);
        return _context.AIRenovationEstimates
            .Where(x => x.UserId == userId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Take(safeTake)
            .ToListAsync();
    }

    public async Task<string> GenerateEstimateNumberAsync()
    {
        var prefix = $"REN-{DateTime.UtcNow:yyyyMMdd}";
        var lastNumber = await _context.AIRenovationEstimates
            .Where(x => x.EstimateNumber.StartsWith(prefix))
            .OrderByDescending(x => x.EstimateNumber)
            .Select(x => x.EstimateNumber)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(lastNumber))
        {
            return $"{prefix}-0001";
        }

        var sequencePart = lastNumber.Split('-').LastOrDefault();
        if (!int.TryParse(sequencePart, out var sequence))
        {
            sequence = 0;
        }

        return $"{prefix}-{(sequence + 1):D4}";
    }
}
