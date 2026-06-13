namespace TBM.Application.DTOs.Products;

public class UpdateProductDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string? SKU { get; set; }
    
    public Guid CategoryId { get; set; }
    
    public decimal? Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public bool ShowPrice { get; set; }
    
    public int? StockQuantity { get; set; }
    public int? LowStockThreshold { get; set; }
    public bool TrackInventory { get; set; }
    
    public bool IsActive { get; set; }
    public bool IsFeatured { get; set; }
    public int DisplayOrder { get; set; }
    
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? Tags { get; set; }
    public string? AIKeywords { get; set; }
    public string? MaterialType { get; set; }
    public string? QualityTier { get; set; }
    public string? RecommendedFor { get; set; }

    // Rich product content
    public List<SpecificationItemDto>? Specifications { get; set; }
    public List<string>? KeyFeatures { get; set; }
    public List<string>? WhatsIncluded { get; set; }
    public List<string>? WhatsNotIncluded { get; set; }

    // Structured attributes
    public string? Dimensions { get; set; }
    public string? Warranty { get; set; }
    public string? FinishType { get; set; }
    public string? InstallationType { get; set; }
    public string? Material { get; set; }
    public string? Color { get; set; }
}
