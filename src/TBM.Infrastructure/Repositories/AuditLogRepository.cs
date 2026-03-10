using Microsoft.EntityFrameworkCore;
using TBM.Core.Entities.Audit;
using TBM.Core.Interfaces.Repositories;
using TBM.Infrastructure.Data;

namespace TBM.Infrastructure.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly ApplicationDbContext _context;

    public AuditLogRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AuditLog log)
    {
        await _context.AuditLogs.AddAsync(log);
    }

    public async Task<(IEnumerable<AuditLog> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        string? search = null,
        string? severity = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : pageSize;

        var query = _context.AuditLogs
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (fromUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAt <= toUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim();
            query = query.Where(x =>
                x.Action.Contains(normalized) ||
                x.Category.Contains(normalized) ||
                (x.UserId != null && x.UserId.Contains(normalized)) ||
                (x.IpAddress != null && x.IpAddress.Contains(normalized)) ||
                (x.NewValue != null && x.NewValue.Contains(normalized)) ||
                (x.OldValue != null && x.OldValue.Contains(normalized)));
        }

        var records = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        if (!string.IsNullOrWhiteSpace(severity))
        {
            var normalizedSeverity = severity.Trim().ToLowerInvariant();
            records = records
                .Where(x => string.Equals(ClassifySeverity(x), normalizedSeverity, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var totalCount = records.Count;

        var items = records
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (items, totalCount);
    }

    public async Task<(int TotalLogs, int ErrorCount, int WarningCount, int InfoCount, DateTime? LastLogAt)> GetStatsAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null)
    {
        var query = _context.AuditLogs
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (fromUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAt <= toUtc.Value);
        }

        var records = await query.ToListAsync();

        var total = records.Count;
        var error = records.Count(x => ClassifySeverity(x) == "error");
        var warning = records.Count(x => ClassifySeverity(x) == "warning");
        var info = total - error - warning;
        var lastLogAt = records.Count == 0
            ? null
            : records.Max(x => (DateTime?)x.CreatedAt);

        return (total, error, warning, info, lastLogAt);
    }

    private static string ClassifySeverity(AuditLog log)
    {
        var text = $"{log.Action} {log.Category} {log.NewValue} {log.OldValue}".ToLowerInvariant();

        if (text.Contains("error") || text.Contains("exception") || text.Contains("failed"))
        {
            return "error";
        }

        if (text.Contains("warn") ||
            text.Contains("suspend") ||
            text.Contains("cancel") ||
            text.Contains("delete") ||
            text.Contains("refund") ||
            text.Contains("deactivate"))
        {
            return "warning";
        }

        return "info";
    }
}
