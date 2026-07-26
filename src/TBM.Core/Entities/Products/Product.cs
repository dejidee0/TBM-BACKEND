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
    public int LowStockThreshold { get; set; } = 5;
    public bool TrackInventory { get; set; } = true;
    
    // Status
    public bool IsActive { get; set; } = true;
    public bool IsFeatured { get; set; }
    public int DisplayOrder { get; set; }
    
    // SEO & Metadata
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? MetaKeywords { get; set; }
    public string? Tags { get; set; }
    public string? AIKeywords { get; set; }
    public string? MaterialType { get; set; }
    public string? QualityTier { get; set; }
    public string? RecommendedFor { get; set; }

    // Rich product content (stored as JSON strings)
    public string? Specifications { get; set; }    // JSON: [{Key, Value}]
    public string? KeyFeatures { get; set; }       // JSON: [string]
    public string? WhatsIncluded { get; set; }     // JSON: [string]
    public string? WhatsNotIncluded { get; set; }  // JSON: [string]

    // Structured product attributes
    public string? Dimensions { get; set; }
    public string? Warranty { get; set; }
    public string? FinishType { get; set; }
    public string? InstallationType { get; set; }
    public string? Material { get; set; }
    public string? Color { get; set; }
    public string? Size { get; set; }

    // Navigation properties
    public Category Category { get; set; } = null!;
    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
}
