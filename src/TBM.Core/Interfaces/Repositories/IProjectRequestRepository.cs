using TBM.Core.Entities.ProjectRequests;

namespace TBM.Core.Interfaces.Repositories;

public interface IProjectRequestRepository
{
    Task<ProjectRequest> CreateAsync(ProjectRequest request);
    Task<ProjectRequest?> GetByIdAsync(Guid id);
}
