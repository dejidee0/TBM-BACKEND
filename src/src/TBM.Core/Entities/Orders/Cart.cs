using TBM.Core.Entities.Common;

namespace TBM.Core.Entities.Orders;

public class Cart : AuditableEntity
{
    public Guid UserId { get; set; }
    public DateTime? ExpiresAt { get; set; }
    
    // Navigation properties
    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
}