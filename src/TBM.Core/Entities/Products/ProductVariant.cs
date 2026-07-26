using TBM.Core.Entities.Common;

namespace TBM.Core.Entities.Products;

public class ProductVariant : BaseEntity
{
    public Guid ProductId { get; set; }
    public string Size { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }

    // Navigation property
    public Product Product { get; set; } = null!;
}
