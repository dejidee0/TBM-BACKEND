namespace TBM.Application.DTOs.Orders;

public class AddToCartDto
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; } = 1;
}