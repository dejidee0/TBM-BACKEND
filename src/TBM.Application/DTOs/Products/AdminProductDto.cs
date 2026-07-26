namespace TBM.Application.DTOs.Products;

/// <summary>
/// Admin-facing product response. Extends the public ProductDto with the
/// write-only SEO fields (MetaTitle/MetaDescription/MetaKeywords) that public
/// GET endpoints never return.
/// </summary>
public class AdminProductDto : ProductDto
{
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? MetaKeywords { get; set; }
}
