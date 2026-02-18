using TBM.Core.Entities.Common;

namespace TBM.Core.Entities.Products;

public class ProductImage : BaseEntity
{
    public Guid ProductId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? AltText { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsPrimary { get; set; }
    
    // Navigation property
    public Product Product { get; set; } = null!;
}