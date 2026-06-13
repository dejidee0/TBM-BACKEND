namespace TBM.Application.DTOs.DesignFlow;

public class BomInventoryProductDto
{
    public Guid ProductId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? AIKeywords { get; set; }
    public string? MaterialType { get; set; }
    public string? QualityTier { get; set; }
    public string? RecommendedFor { get; set; }
    public decimal? Price { get; set; }
    public int? StockQuantity { get; set; }
    public string? CategoryName { get; set; }
}

public class BomGenerationRequestDto
{
    public Guid SessionId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string RoomType { get; set; } = string.Empty;
    public string VisionText { get; set; } = string.Empty;
    public string Tier { get; set; } = string.Empty;
    public decimal RoomLength { get; set; }
    public decimal RoomWidth { get; set; }
    public decimal RoomHeight { get; set; }
    public List<BomInventoryProductDto> Inventory { get; set; } = new();
}

public class BomGenerationItemDto
{
    public string SKU { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int? LeadTimeDays { get; set; }
}

public class BomGenerationResultDto
{
    public List<BomGenerationItemDto> Items { get; set; } = new();
    public string? Notes { get; set; }
}
