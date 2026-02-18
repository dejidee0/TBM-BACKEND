using TBM.Core.Entities.Common;

namespace TBM.Core.Entities.Audit;

public class AuditLog : AuditableEntity
{
    public string UserId { get; set; } = null!;
    public string Action { get; set; } = null!;
    public string Category { get; set; } = null!;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? IpAddress { get; set; }
}
