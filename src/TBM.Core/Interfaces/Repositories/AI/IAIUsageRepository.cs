using TBM.Core.Entities.AI;
using TBM.Core.Models.AI;

namespace TBM.Core.Interfaces.Repositories.AI;

public interface IAIUsageRepository
{
    Task CreateAsync(AIUsage usage);
    Task<AIUsageSummary> GetUserSummaryAsync(Guid userId, DateTime fromUtc, DateTime toUtc);
    Task<List<AIUsageMonthlySpend>> GetMonthlySpendAsync(DateTime fromUtc, DateTime toUtc);
}
