namespace TBM.Application.DTOs.Products;

public class BulkCreateProductResultDto
{
    public int TotalSubmitted { get; set; }
    public int Created { get; set; }
    public int Failed { get; set; }
    public List<BulkProductFailureDto> Failures { get; set; } = new();
    public List<ProductDto> CreatedProducts { get; set; } = new();
}

public class BulkProductFailureDto
{
    public int Index { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
