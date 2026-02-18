namespace TBM.Application.DTOs.Orders;

public class UpdatePaymentStatusDto
{
    public int PaymentStatus { get; set; }
    public int? PaymentMethod { get; set; }
    public string? PaymentReference { get; set; }
}