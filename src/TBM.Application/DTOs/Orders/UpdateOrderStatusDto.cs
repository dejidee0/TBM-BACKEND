namespace TBM.Application.DTOs.Orders;

public class UpdateOrderStatusDto
{
    public int Status { get; set; }
    public string? AdminNotes { get; set; }
    public string? TrackingNumber { get; set; }
}