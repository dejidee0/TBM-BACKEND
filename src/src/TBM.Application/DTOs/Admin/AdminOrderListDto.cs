namespace TBM.Application.DTOs.Admin;

public class AdminOrderListDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = null!;
    public string CustomerName { get; set; } = null!;
    public decimal Total { get; set; }
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
