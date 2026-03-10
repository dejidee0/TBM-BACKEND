
using TBM.Core.Entities.Audit;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog log);
    Task<(IEnumerable<AuditLog> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        string? search = null,
        string? severity = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null);
    Task<(int TotalLogs, int ErrorCount, int WarningCount, int InfoCount, DateTime? LastLogAt)> GetStatsAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null);
}
