using Microsoft.EntityFrameworkCore;
using TBM.Core.Entities.AI;
using TBM.Core.Interfaces.Repositories.AI;
using TBM.Infrastructure.Data;

namespace TBM.Infrastructure.Repositories.AI
{
    public class AIDesignRepository : IAIDesignRepository
    {
        private readonly ApplicationDbContext _context;

        public AIDesignRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task CreateAsync(AIDesign design)
        {
            await _context.AIDesigns.AddAsync(design);
        }

        public Task<AIDesign?> GetByIdAsync(Guid id)
        {
            return _context.AIDesigns
                .Include(x => x.AIProject)
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public Task<List<AIDesign>> GetByProjectAsync(Guid projectId)
        {
            return _context.AIDesigns
                .Where(x => x.AIProjectId == projectId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
        }
    }
}
