namespace TBM.Application.DTOs.Products;

public class ImportProductDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string? SKU { get; set; }

    public string BrandType { get; set; } = string.Empty;
    public string ProductType { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;

    public decimal? Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public bool ShowPrice { get; set; } = true;

    public int? StockQuantity { get; set; }
    public int? LowStockThreshold { get; set; }
    public bool TrackInventory { get; set; } = true;

    public bool IsActive { get; set; } = true;
    public bool IsFeatured { get; set; }
    public int DisplayOrder { get; set; }

    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? Tags { get; set; }
    public string? AIKeywords { get; set; }
    public string? MaterialType { get; set; }
    public string? QualityTier { get; set; }
    public string? RecommendedFor { get; set; }

    // Rich content — pipe-separated in CSV (e.g., "Feature 1|Feature 2")
    // Specifications use "Key:Value|Key2:Value2" format
    public string? Specifications { get; set; }
    public string? KeyFeatures { get; set; }
    public string? WhatsIncluded { get; set; }
    public string? WhatsNotIncluded { get; set; }

    // Plain string fields
    public string? Dimensions { get; set; }
    public string? Warranty { get; set; }
    public string? FinishType { get; set; }
    public string? InstallationType { get; set; }
    public string? Material { get; set; }
    public string? Color { get; set; }
}
