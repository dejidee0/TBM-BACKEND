namespace TBM.Application.DTOs.Orders;

public class OrderDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string UserFullName { get; set; } = string.Empty;
    
    // Order Status
    public int Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public int PaymentStatus { get; set; }
    public string PaymentStatusName { get; set; } = string.Empty;
    public int? PaymentMethod { get; set; }
    public string? PaymentMethodName { get; set; }
    
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
    
    // Items
    public List<OrderItemDto> Items { get; set; } = new();
    
    // Timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}