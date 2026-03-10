using Microsoft.EntityFrameworkCore;
using TBM.Core.Entities.AI;
using TBM.Core.Interfaces.Repositories.AI;
using TBM.Infrastructure.Data;

namespace TBM.Infrastructure.Repositories.AI;

public class AIAssistantRepository : IAIAssistantRepository
{
    private readonly ApplicationDbContext _context;

    public AIAssistantRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task CreateSessionAsync(AIAssistantSession session)
    {
        await _context.AIAssistantSessions.AddAsync(session);
    }

    public Task<AIAssistantSession?> GetSessionAsync(Guid sessionId, Guid userId)
    {
        return _context.AIAssistantSessions
            .Include(x => x.Messages.OrderBy(m => m.CreatedAt))
            .Include(x => x.Tasks.OrderByDescending(t => t.CreatedAt))
            .Include(x => x.ToolActions.OrderByDescending(a => a.CreatedAt))
                .ThenInclude(a => a.Approvals.OrderByDescending(p => p.CreatedAt))
            .Include(x => x.ToolActions.OrderByDescending(a => a.CreatedAt))
                .ThenInclude(a => a.Executions.OrderByDescending(e => e.CreatedAt))
            .FirstOrDefaultAsync(x =>
                x.Id == sessionId &&
                x.UserId == userId &&
                !x.IsDeleted);
    }

    public Task<List<AIAssistantSession>> GetSessionsAsync(Guid userId, int take = 20)
    {
        var safeTake = take < 1 ? 20 : Math.Min(take, 100);
        return _context.AIAssistantSessions
            .Include(x => x.Messages)
            .Include(x => x.Tasks)
            .Where(x => x.UserId == userId && !x.IsDeleted)
            .OrderByDescending(x => x.LastUpdatedAtUtc)
            .Take(safeTake)
            .ToListAsync();
    }

    public async Task AddMessageAsync(AIAssistantMessage message)
    {
        await _context.AIAssistantMessages.AddAsync(message);
    }

    public async Task AddTaskAsync(AIAssistantTask task)
    {
        await _context.AIAssistantTasks.AddAsync(task);
    }

    public async Task AddToolActionAsync(AIAssistantToolAction toolAction)
    {
        await _context.AIAssistantToolActions.AddAsync(toolAction);
    }

    public async Task AddToolApprovalAsync(AIAssistantToolApproval approval)
    {
        await _context.AIAssistantToolApprovals.AddAsync(approval);
    }

    public async Task AddToolExecutionAsync(AIAssistantToolExecution execution)
    {
        await _context.AIAssistantToolExecutions.AddAsync(execution);
    }

    public Task<AIAssistantTask?> GetTaskAsync(Guid taskId, Guid userId)
    {
        return _context.AIAssistantTasks
            .Include(x => x.Session)
            .Include(x => x.ToolAction)
            .FirstOrDefaultAsync(x =>
                x.Id == taskId &&
                !x.IsDeleted &&
                x.Session.UserId == userId &&
                !x.Session.IsDeleted);
    }

    public Task<AIAssistantToolAction?> GetToolActionAsync(Guid actionId, Guid userId)
    {
        return _context.AIAssistantToolActions
            .Include(x => x.Session)
                .ThenInclude(s => s.Tasks)
            .Include(x => x.Message)
            .Include(x => x.Approvals.OrderByDescending(a => a.CreatedAt))
            .Include(x => x.Executions.OrderByDescending(e => e.CreatedAt))
            .FirstOrDefaultAsync(x =>
                x.Id == actionId &&
                !x.IsDeleted &&
                x.Session.UserId == userId &&
                !x.Session.IsDeleted);
    }

    public Task<AIAssistantToolApproval?> GetLatestApprovalAsync(Guid actionId, Guid userId)
    {
        return _context.AIAssistantToolApprovals
            .Where(x =>
                x.ToolActionId == actionId &&
                x.UserId == userId &&
                !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();
    }
}
