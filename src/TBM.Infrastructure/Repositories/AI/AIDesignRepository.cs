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

        public async Task<(IReadOnlyList<AIDesign> Items, int TotalCount)> GetPublicPagedAsync(
            int page,
            int limit,
            string? roomType,
            string? search,
            string? sort)
        {
            var safePage = page < 1 ? 1 : page;
            var safeLimit = limit < 1 ? 12 : Math.Min(limit, 50);

            var query = _context.AIDesigns
                .Include(x => x.AIProject)
                .Where(x =>
                    !x.IsDeleted &&
                    x.IsPublic &&
                    !x.AIProject.IsDeleted);

            if (!string.IsNullOrWhiteSpace(roomType))
            {
                query = query.Where(x =>
                    x.AIProject.ContextLabel != null &&
                    x.AIProject.ContextLabel.Contains(roomType));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(x =>
                    (x.AIProject.Prompt != null && x.AIProject.Prompt.Contains(search)) ||
                    (x.AIProject.ContextLabel != null && x.AIProject.ContextLabel.Contains(search)));
            }

            query = sort?.Trim().ToLowerInvariant() switch
            {
                "oldest" => query.OrderBy(x => x.PublishedAt ?? x.CreatedAt),
                _ => query.OrderByDescending(x => x.PublishedAt ?? x.CreatedAt)
            };

            var total = await query.CountAsync();
            var items = await query
                .Skip((safePage - 1) * safeLimit)
                .Take(safeLimit)
                .ToListAsync();

            return (items, total);
        }
    }
}
