using TBM.Application.DTOs.Orders;

namespace TBM.Application.DTOs.Admin;

public class AdminOrderDetailsDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = null!;
    public string Status { get; set; } = null!;
    public decimal Total { get; set; }

    public List<OrderItemDto> Items { get; set; } = new();
    public List<OrderStatusHistoryDto> History { get; set; } = new();
}
