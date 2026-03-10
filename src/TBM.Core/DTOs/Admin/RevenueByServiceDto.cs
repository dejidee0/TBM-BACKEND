namespace TBM.Core.DTOs.Admin;

public class RevenueByServiceDto
{
    public string Service { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int Orders { get; set; }
}
