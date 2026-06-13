using TBM.Core.Entities.Common;

namespace TBM.Core.Entities.DesignFlow;

public class BOMItem : AuditableEntity
{
    public Guid BOMId { get; set; }
    public Guid ProductId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public bool InStock { get; set; }
    public Guid? VendorId { get; set; }
    public int? LeadTimeDays { get; set; }
    public string Reason { get; set; } = string.Empty;
}
