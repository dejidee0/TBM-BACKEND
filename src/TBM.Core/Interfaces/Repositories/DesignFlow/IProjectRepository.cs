using TBM.Core.Entities.DesignFlow;

namespace TBM.Core.Interfaces.Repositories.DesignFlow;

public interface IProjectRepository
{
    Task<Project> CreateAsync(Project project);
    Task<Project?> GetByIdAsync(Guid id);
    Task<Project?> GetByOrderIdAsync(Guid orderId);
    Task<Project?> GetByDesignSessionIdAsync(Guid designSessionId);
    Task<List<Project>> GetByUserAsync(Guid userId);
    Task UpdateAsync(Project project);
    Task AddTimelineAsync(ProjectTimeline timeline);
    Task AddDocumentAsync(ProjectDocument document);
    Task AddGalleryImageAsync(SiteGalleryImage image);
    Task<string> GenerateProjectNumberAsync();
}
