
using TBM.Core.Entities.Audit;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog log);
}
