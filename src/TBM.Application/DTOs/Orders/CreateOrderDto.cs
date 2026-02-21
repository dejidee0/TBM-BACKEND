namespace TBM.Application.DTOs.Orders;

public class CreateOrderDto
{
    public string ShippingFullName { get; set; } = string.Empty;
    public string ShippingPhone { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public string ShippingCity { get; set; } = string.Empty;
    public string ShippingState { get; set; } = string.Empty;
    public string? ShippingNotes { get; set; }
    public string? CustomerNotes { get; set; }
    public string? PromoCode { get; set; }
    public decimal? ShippingCost { get; set; }
    public decimal? Tax { get; set; }
    public decimal? Discount { get; set; }
}
