using TBM.Core.Entities.Common;
using TBM.Core.Entities.Users;
using TBM.Core.Enums;


namespace TBM.Core.Entities.Orders;

public class Order : AuditableEntity
{
    public string OrderNumber { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    
    // Order Status
    public OrderStatus Status { get; set; }
    public string? AdminNote { get; set; }

    public PaymentStatus PaymentStatus { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    
    // Pricing
    public decimal SubTotal { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal Tax { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
    
    // Shipping Details
    public string ShippingFullName { get; set; } = string.Empty;
    public string ShippingPhone { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public string ShippingCity { get; set; } = string.Empty;
    public string ShippingState { get; set; } = string.Empty;
    public string? ShippingNotes { get; set; }
    
    // Payment Details
    public string? PaymentReference { get; set; }
    public DateTime? PaidAt { get; set; }
    
    // Tracking
    public string? TrackingNumber { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    
    // Notes
    public string? CustomerNotes { get; set; }
    public string? AdminNotes { get; set; }
    
    // Navigation properties
    public User User { get; set; } = null!;
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}