using Microsoft.EntityFrameworkCore;
using TBM.Core.Entities.AI;
using TBM.Core.Interfaces.Repositories.AI;
using TBM.Infrastructure.Data;

namespace TBM.Infrastructure.Repositories.AI
{
    public class AIProjectRepository : IAIProjectRepository
    {
        private readonly ApplicationDbContext _context;

        public AIProjectRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task CreateAsync(AIProject project)
        {
            await _context.AIProjects.AddAsync(project);
        }

        public Task<AIProject?> GetByIdAsync(Guid id)
        {
            return _context.AIProjects
                .Include(x => x.Designs)
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public Task<List<AIProject>> GetByUserAsync(Guid userId)
        {
            return _context.AIProjects
                .Include(x => x.Designs)
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
        }
    }
}
