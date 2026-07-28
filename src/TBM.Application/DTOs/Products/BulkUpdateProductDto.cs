namespace TBM.Application.DTOs.Products;

public class BulkUpdateProductItemDto : UpdateProductDto
{
    public Guid Id { get; set; }
}

public class BulkUpdateProductResultDto
{
    public int TotalSubmitted { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public List<BulkProductFailureDto> Failures { get; set; } = new();
    public List<ProductDto> UpdatedProducts { get; set; } = new();
}
