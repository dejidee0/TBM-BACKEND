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
    
    public string? Tags { get; set; }
    
    public List<ProductImageDto> Images { get; set; } = new();
    public string? PrimaryImageUrl { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}