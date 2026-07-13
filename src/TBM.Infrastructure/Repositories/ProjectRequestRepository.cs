using Microsoft.EntityFrameworkCore;
using TBM.Core.Entities.ProjectRequests;
using TBM.Core.Interfaces.Repositories;
using TBM.Infrastructure.Data;

namespace TBM.Infrastructure.Repositories;

public class ProjectRequestRepository : IProjectRequestRepository
{
    private readonly ApplicationDbContext _context;

    public ProjectRequestRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ProjectRequest> CreateAsync(ProjectRequest request)
    {
        await _context.ProjectRequests.AddAsync(request);
        await _context.SaveChangesAsync();
        return request;
    }

    public Task<ProjectRequest?> GetByIdAsync(Guid id)
    {
        return _context.ProjectRequests.FirstOrDefaultAsync(x => x.Id == id);
    }
}
