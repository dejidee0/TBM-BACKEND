using Microsoft.EntityFrameworkCore;
using TBM.Core.Entities.AI;
using TBM.Core.Interfaces.Repositories.AI;
using TBM.Core.Models.AI;
using TBM.Infrastructure.Data;

namespace TBM.Infrastructure.Repositories.AI;

public class AIUsageRepository : IAIUsageRepository
{
    private readonly ApplicationDbContext _context;

    public AIUsageRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task CreateAsync(AIUsage usage)
    {
        await _context.AIUsages.AddAsync(usage);
    }

    public async Task<AIUsageSummary> GetUserSummaryAsync(Guid userId, DateTime fromUtc, DateTime toUtc)
    {
        var query = _context.AIUsages
            .Where(x => x.UserId == userId &&
                        !x.IsDeleted &&
                        x.CreatedAt >= fromUtc &&
                        x.CreatedAt < toUtc);

        return new AIUsageSummary
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            TotalGenerations = await query.CountAsync(),
            TotalCreditsUsed = await query.SumAsync(x => (int?)x.CreditsUsed) ?? 0,
            TotalEstimatedCost = await query.SumAsync(x => (decimal?)x.EstimatedCost) ?? 0m
        };
    }

    public async Task<List<AIUsageMonthlySpend>> GetMonthlySpendAsync(DateTime fromUtc, DateTime toUtc)
    {
        return await _context.AIUsages
            .Where(x => !x.IsDeleted &&
                        x.CreatedAt >= fromUtc &&
                        x.CreatedAt < toUtc)
            .GroupBy(x => new { x.CreatedAt.Year, x.CreatedAt.Month })
            .Select(g => new AIUsageMonthlySpend
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                TotalGenerations = g.Count(),
                TotalCreditsUsed = g.Sum(x => x.CreditsUsed),
                TotalEstimatedCost = g.Sum(x => x.EstimatedCost),
                DistinctUsers = g.Select(x => x.UserId).Distinct().Count()
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ToListAsync();
    }
}
