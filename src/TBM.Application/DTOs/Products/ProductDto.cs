namespace TBM.Application.DTOs.Products;

public class ProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? SKU { get; set; }
    
    public int BrandType { get; set; }
    public string BrandName { get; set; } = string.Empty;
    public int ProductType { get; set; }
    public string ProductTypeName { get; set; } = string.Empty;
    
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    
    public decimal? Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public bool ShowPrice { get; set; }
    public string PriceDisplay { get; set; } = string.Empty;
    
    public int? StockQuantity { get; set; }
    public bool InStock { get; set; }
    public bool TrackInventory { get; set; }
    
    public bool IsActive { get; set; }
    public bool IsFeatured { get; set; }
    public int DisplayOrder { get; set; }
    public int LowStockThreshold { get; set; }

    public string? Tags { get; set; }
    public string? AIKeywords { get; set; }
    public string? MaterialType { get; set; }
    public string? QualityTier { get; set; }
    public string? RecommendedFor { get; set; }

    // Rich product content (deserialized from JSON)
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
    public string? Size { get; set; }

    public List<ProductVariantDto> Variants { get; set; } = new();

    public List<ProductImageDto> Images { get; set; } = new();
    public string? PrimaryImageUrl { get; set; }
    public List<ProductCardDto> SimilarProducts { get; set; } = new();
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SpecificationItemDto
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class ProductCardDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public string? Image { get; set; }
    public string Category { get; set; } = string.Empty;
    public bool InStock { get; set; }
}
