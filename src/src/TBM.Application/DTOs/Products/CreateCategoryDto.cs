namespace TBM.Application.DTOs.Products;

public class CreateCategoryDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int BrandType { get; set; }
    public Guid? ParentCategoryId { get; set; }
    public string? ImageUrl { get; set; }
    public int DisplayOrder { get; set; }
}