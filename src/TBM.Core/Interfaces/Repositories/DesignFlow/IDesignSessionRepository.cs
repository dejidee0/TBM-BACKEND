using TBM.Core.Entities.DesignFlow;

namespace TBM.Core.Interfaces.Repositories.DesignFlow;

public interface IDesignSessionRepository
{
    Task<DesignSession> CreateAsync(DesignSession session);
    Task<DesignSession?> GetByIdAsync(Guid id);
    Task<List<DesignSession>> GetByUserAsync(Guid userId);
    Task<DesignSession?> GetNextProcessingAsync();
    Task UpdateAsync(DesignSession session);
    Task<string> GenerateSessionNumberAsync();
}
