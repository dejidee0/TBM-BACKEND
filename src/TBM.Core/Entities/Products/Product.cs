using TBM.Core.Entities.Common;
using TBM.Core.Enums;

namespace TBM.Core.Entities.Products;

public class Product : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? SKU { get; set; }
    
    public BrandType BrandType { get; set; }
    public ProductType ProductType { get; set; }
    
    public Guid CategoryId { get; set; }
    
    // Pricing
    public decimal? Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public bool ShowPrice { get; set; } = true; // False for "Request Price"
    
    // Inventory (mainly for Bogat products)
    public int? StockQuantity { get; set; }
    public int? LowStockThreshold { get; set; }
    public bool TrackInventory { get; set; } = true;
    
    // Status
    public bool IsActive { get; set; } = true;
    public bool IsFeatured { get; set; }
    public int DisplayOrder { get; set; }
    
    // SEO & Metadata
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? Tags { get; set; } // Comma-separated tags
    
    // Navigation properties
    public Category Category { get; set; } = null!;
    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
}