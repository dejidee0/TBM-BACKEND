namespace TBM.Application.DTOs.Products;

public class ProductFilterDto
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 12;
    public int? BrandType { get; set; }
    public int? ProductType { get; set; }
    public Guid? CategoryId { get; set; }
    public string? SearchTerm { get; set; }
    public bool? IsFeatured { get; set; }
    public bool ActiveOnly { get; set; } = true;
}