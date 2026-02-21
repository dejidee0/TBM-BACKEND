using TBM.Core.Enums;

namespace TBM.Application.DTOs.Vendor;

public class VendorDashboardDto
{
    public int OwnedProducts { get; set; }
    public int LowStockProducts { get; set; }
    public int AssignedOrders { get; set; }
    public int PendingOrders { get; set; }
    public int InProgressOrders { get; set; }
    public int UnreadNotifications { get; set; }
    public int UnreadMessages { get; set; }
}

public class VendorAlertDto
{
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? ProductId { get; set; }
    public Guid? OrderId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class VendorActivityDto
{
    public string Id { get; set; } = string.Empty;
    public string ActivityType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? OrderId { get; set; }
    public Guid? ProductId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class VendorOrderListItemDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
    public int MatchingItems { get; set; }
    public string? DeliveryAgentName { get; set; }
    public string? DeliveryAgentPhone { get; set; }
}

public class VendorOrderNoteDto
{
    public string Id { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public Guid VendorId { get; set; }
    public string Note { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public class VendorOrderDetailDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public string ShippingCity { get; set; } = string.Empty;
    public string ShippingState { get; set; } = string.Empty;
    public string ShippingPhone { get; set; } = string.Empty;
    public string? DeliveryAgentName { get; set; }
    public string? DeliveryAgentPhone { get; set; }
    public List<VendorOrderItemDto> Items { get; set; } = new();
    public List<VendorOrderNoteDto> Notes { get; set; } = new();
}

public class VendorOrderItemDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal SubTotal { get; set; }
}

public class VendorOrderStatusUpdateRequest
{
    public OrderStatus Status { get; set; }
    public string? Note { get; set; }
}

public class VendorOrderNoteRequest
{
    public string Note { get; set; } = string.Empty;
}

public class VendorOrderAssignmentRequest
{
    public string? DeliveryAgentName { get; set; }
    public string? DeliveryAgentPhone { get; set; }
    public string? AssignmentNote { get; set; }
}

public class VendorInventoryItemDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? SKU { get; set; }
    public int? StockQuantity { get; set; }
    public int? LowStockThreshold { get; set; }
    public bool IsLowStock { get; set; }
    public bool IsActive { get; set; }
    public decimal? Price { get; set; }
}

public class VendorInventoryUpdateRequest
{
    public int StockQuantity { get; set; }
    public int? LowStockThreshold { get; set; }
    public bool? IsActive { get; set; }
    public decimal? Price { get; set; }
}

public class VendorDeliveryAssignmentDto
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public string? TrackingNumber { get; set; }
    public string? DeliveryAgentName { get; set; }
    public string? DeliveryAgentPhone { get; set; }
    public string? AssignmentNote { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class VendorMessageDto
{
    public string Id { get; set; } = string.Empty;
    public Guid VendorId { get; set; }
    public string Direction { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? From { get; set; }
    public string? To { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class VendorMessageCreateRequest
{
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? To { get; set; }
}

public class VendorNotificationDto
{
    public string Id { get; set; } = string.Empty;
    public Guid VendorId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public Guid? OrderId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class VendorPagedResultDto<T>
{
    public List<T> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class ActivateVendorRequest
{
    public string? DisplayName { get; set; }
    public int? SlaHours { get; set; }
    public string? TenantId { get; set; }
}

public class AssignProductOwnerRequest
{
    public Guid VendorUserId { get; set; }
}

public class VendorProductOwnershipDto
{
    public Guid ProductId { get; set; }
    public Guid VendorUserId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Guid AssignedByUserId { get; set; }
    public DateTime AssignedAtUtc { get; set; }
}
