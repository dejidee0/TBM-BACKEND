using TBM.Core.Entities.Inspiration;

namespace TBM.Core.Interfaces.Repositories;

public interface IInspirationDesignRepository
{
    Task<List<InspirationDesign>> GetActiveAsync(string? category, string? style);
}
