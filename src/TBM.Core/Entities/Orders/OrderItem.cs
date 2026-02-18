using TBM.Core.Entities.Common;
using TBM.Core.Entities.Products;

namespace TBM.Core.Entities.Orders;

public class OrderItem : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    
    public string ProductName { get; set; } = string.Empty;
    public string? ProductSKU { get; set; }
    public string? ProductImageUrl { get; set; }
    
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal SubTotal { get; set; }
    
    // Navigation properties
    public Order Order { get; set; } = null!;
    public Product Product { get; set; } = null!;
}