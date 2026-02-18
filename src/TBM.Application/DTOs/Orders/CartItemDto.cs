namespace TBM.Application.DTOs.Orders;

public class CartItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductSKU { get; set; }
    public string? ProductImageUrl { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal SubTotal { get; set; }
    public bool InStock { get; set; }
    public int? StockQuantity { get; set; }
    public DateTime AddedAt { get; set; }
}