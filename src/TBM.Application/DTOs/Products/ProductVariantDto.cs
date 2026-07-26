namespace TBM.Application.DTOs.Products;

public class ProductVariantDto
{
    public Guid Id { get; set; }
    public string Size { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
}

public class CreateProductVariantDto
{
    public string Size { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}
