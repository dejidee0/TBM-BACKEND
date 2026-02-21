using TBM.Core.Entities.AI;

namespace TBM.Core.Interfaces.Repositories.AI
{
    public interface IAIDesignRepository
    {
        Task CreateAsync(AIDesign design);
        Task<AIDesign?> GetByIdAsync(Guid id);
        Task<List<AIDesign>> GetByProjectAsync(Guid projectId);
    }
}
