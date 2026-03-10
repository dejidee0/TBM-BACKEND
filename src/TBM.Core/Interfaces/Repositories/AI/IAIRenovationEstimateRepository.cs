using TBM.Core.Entities.AI;

namespace TBM.Core.Interfaces.Repositories.AI;

public interface IAIRenovationEstimateRepository
{
    Task CreateAsync(AIRenovationEstimate estimate);
    Task<AIRenovationEstimate?> GetByIdAsync(Guid estimateId, Guid userId);
    Task<List<AIRenovationEstimate>> GetByUserAsync(Guid userId, int take = 100);
    Task<string> GenerateEstimateNumberAsync();
}
