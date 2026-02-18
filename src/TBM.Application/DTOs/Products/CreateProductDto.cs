namespace TBM.Application.DTOs.Products;

public class CreateProductDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string? SKU { get; set; }
    
    public int BrandType { get; set; }
    public int ProductType { get; set; }
    
    public Guid CategoryId { get; set; }
    
    public decimal? Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public bool ShowPrice { get; set; } = true;
    
    public int? StockQuantity { get; set; }
    public int? LowStockThreshold { get; set; }
    public bool TrackInventory { get; set; } = true;
    
    public bool IsFeatured { get; set; }
    public int DisplayOrder { get; set; }
    
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? Tags { get; set; }
}