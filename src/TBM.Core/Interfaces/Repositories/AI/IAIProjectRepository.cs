using TBM.Core.Entities.AI;

namespace TBM.Core.Interfaces.Repositories.AI
{
    public interface IAIProjectRepository
    {
        Task CreateAsync(AIProject project);
        Task<AIProject?> GetByIdAsync(Guid id);
        Task<List<AIProject>> GetByUserAsync(Guid userId);
    }
}
