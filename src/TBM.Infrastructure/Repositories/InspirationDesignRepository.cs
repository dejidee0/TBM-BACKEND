using Microsoft.EntityFrameworkCore;
using TBM.Core.Entities.Inspiration;
using TBM.Core.Interfaces.Repositories;
using TBM.Infrastructure.Data;

namespace TBM.Infrastructure.Repositories;

public class InspirationDesignRepository : IInspirationDesignRepository
{
    private readonly ApplicationDbContext _context;

    public InspirationDesignRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<InspirationDesign>> GetActiveAsync(string? category, string? style)
    {
        var query = _context.InspirationDesigns.Where(x => x.IsActive);

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(x => x.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(style))
        {
            query = query.Where(x => x.Style == style);
        }

        return await query.OrderBy(x => x.DisplayOrder).ToListAsync();
    }
}
