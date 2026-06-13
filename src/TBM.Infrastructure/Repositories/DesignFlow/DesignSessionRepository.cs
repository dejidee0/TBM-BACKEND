using Microsoft.EntityFrameworkCore;
using TBM.Core.Entities.DesignFlow;
using TBM.Core.Interfaces.Repositories.DesignFlow;
using TBM.Infrastructure.Data;

namespace TBM.Infrastructure.Repositories.DesignFlow;

public class DesignSessionRepository : IDesignSessionRepository
{
    private readonly ApplicationDbContext _context;

    public DesignSessionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DesignSession> CreateAsync(DesignSession session)
    {
        await _context.DesignSessions.AddAsync(session);
        return session;
    }

    public Task<DesignSession?> GetByIdAsync(Guid id)
    {
        return _context.DesignSessions
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
    }

    public Task<List<DesignSession>> GetByUserAsync(Guid userId)
    {
        return _context.DesignSessions
            .Where(x => x.UserId == userId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public Task<DesignSession?> GetNextProcessingAsync()
    {
        return _context.DesignSessions
            .Where(x => x.Status == TBM.Core.Enums.DesignSessionStatus.Processing && !x.IsDeleted)
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public Task UpdateAsync(DesignSession session)
    {
        _context.DesignSessions.Update(session);
        return Task.CompletedTask;
    }

    public async Task<string> GenerateSessionNumberAsync()
    {
        var prefix = $"DS-{DateTime.UtcNow:yyyy}-";
        var lastNumber = await _context.DesignSessions
            .Where(x => x.SessionNumber.StartsWith(prefix))
            .OrderByDescending(x => x.SessionNumber)
            .Select(x => x.SessionNumber)
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
