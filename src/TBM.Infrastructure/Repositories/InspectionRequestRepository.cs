using Microsoft.EntityFrameworkCore;
using TBM.Core.Entities.Inspections;
using TBM.Core.Interfaces.Repositories;
using TBM.Infrastructure.Data;

namespace TBM.Infrastructure.Repositories;

public class InspectionRequestRepository : IInspectionRequestRepository
{
    private readonly ApplicationDbContext _context;

    public InspectionRequestRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<InspectionRequest> CreateAsync(InspectionRequest request)
    {
        await _context.InspectionRequests.AddAsync(request);
        await _context.SaveChangesAsync();
        return request;
    }

    public Task<InspectionRequest?> GetByIdAsync(Guid id)
    {
        return _context.InspectionRequests.FirstOrDefaultAsync(x => x.Id == id);
    }
}
