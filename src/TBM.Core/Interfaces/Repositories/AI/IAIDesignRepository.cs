using TBM.Core.Entities.AI;

namespace TBM.Core.Interfaces.Repositories.AI
{
    public interface IAIDesignRepository
    {
        Task CreateAsync(AIDesign design);
        Task<AIDesign?> GetByIdAsync(Guid id);
        Task<List<AIDesign>> GetByProjectAsync(Guid projectId);
        Task<(IReadOnlyList<AIDesign> Items, int TotalCount)> GetPublicPagedAsync(
            int page,
            int limit,
            string? roomType,
            string? search,
            string? sort);
    }
}
