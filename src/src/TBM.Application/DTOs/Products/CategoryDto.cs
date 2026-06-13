namespace TBM.Application.DTOs.Products;

public class CategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int BrandType { get; set; }
    public string BrandName { get; set; } = string.Empty;
    public Guid? ParentCategoryId { get; set; }
    public string? ParentCategoryName { get; set; }
    public string? ImageUrl { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    public List<CategoryDto> SubCategories { get; set; } = new();
    public int ProductCount { get; set; }
}