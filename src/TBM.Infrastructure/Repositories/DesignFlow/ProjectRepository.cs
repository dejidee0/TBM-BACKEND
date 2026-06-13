using Microsoft.EntityFrameworkCore;
using TBM.Core.Entities.DesignFlow;
using TBM.Core.Interfaces.Repositories.DesignFlow;
using TBM.Infrastructure.Data;

namespace TBM.Infrastructure.Repositories.DesignFlow;

public class ProjectRepository : IProjectRepository
{
    private readonly ApplicationDbContext _context;

    public ProjectRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Project> CreateAsync(Project project)
    {
        await _context.Projects.AddAsync(project);
        return project;
    }

    public Task<Project?> GetByIdAsync(Guid id)
    {
        return _context.Projects
            .Include(x => x.Timelines.OrderBy(t => t.SortOrder))
            .Include(x => x.Documents.OrderByDescending(d => d.UploadedAt))
            .Include(x => x.GalleryImages.OrderBy(g => g.SortOrder))
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
    }

    public Task<Project?> GetByOrderIdAsync(Guid orderId)
    {
        return _context.Projects
            .Include(x => x.Timelines.OrderBy(t => t.SortOrder))
            .Include(x => x.Documents.OrderByDescending(d => d.UploadedAt))
            .Include(x => x.GalleryImages.OrderBy(g => g.SortOrder))
            .FirstOrDefaultAsync(x => x.OrderId == orderId && !x.IsDeleted);
    }

    public Task<Project?> GetByDesignSessionIdAsync(Guid designSessionId)
    {
        return _context.Projects
            .Include(x => x.Timelines.OrderBy(t => t.SortOrder))
            .Include(x => x.Documents.OrderByDescending(d => d.UploadedAt))
            .Include(x => x.GalleryImages.OrderBy(g => g.SortOrder))
            .FirstOrDefaultAsync(x => x.DesignSessionId == designSessionId && !x.IsDeleted);
    }

    public Task<List<Project>> GetByUserAsync(Guid userId)
    {
        return _context.Projects
            .Include(x => x.Timelines.OrderBy(t => t.SortOrder))
            .Include(x => x.Documents.OrderByDescending(d => d.UploadedAt))
            .Include(x => x.GalleryImages.OrderBy(g => g.SortOrder))
            .Where(x => x.UserId == userId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public Task UpdateAsync(Project project)
    {
        _context.Projects.Update(project);
        return Task.CompletedTask;
    }

    public async Task AddTimelineAsync(ProjectTimeline timeline)
    {
        await _context.ProjectTimelines.AddAsync(timeline);
    }

    public async Task AddDocumentAsync(ProjectDocument document)
    {
        await _context.ProjectDocuments.AddAsync(document);
    }

    public async Task AddGalleryImageAsync(SiteGalleryImage image)
    {
        await _context.SiteGalleryImages.AddAsync(image);
    }

    public async Task<string> GenerateProjectNumberAsync()
    {
        var prefix = $"PRJ-{DateTime.UtcNow:yyyy}-";
        var lastNumber = await _context.Projects
            .Where(x => x.ProjectNumber.StartsWith(prefix))
            .OrderByDescending(x => x.ProjectNumber)
            .Select(x => x.ProjectNumber)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(lastNumber))
        {
            return $"{prefix}00001";
        }

        var seqText = lastNumber[(prefix.Length)..];
        return int.TryParse(seqText, out var seq)
            ? $"{prefix}{(seq + 1):D5}"
            : $"{prefix}00001";
    }
}
