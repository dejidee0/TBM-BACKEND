using TBM.Core.Entities.Common;

namespace TBM.Core.Entities.Orders;

public class OrderStatusHistory : AuditableEntity
{


    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public string OldStatus { get; set; } = null!;
    public string NewStatus { get; set; } = null!;

    
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    public string? Note { get; set; }
}
