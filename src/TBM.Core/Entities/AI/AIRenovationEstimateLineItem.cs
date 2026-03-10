using TBM.Core.Entities.Common;

namespace TBM.Core.Entities.AI;

public class AIRenovationEstimateLineItem : AuditableEntity
{
    public Guid EstimateId { get; set; }
    public string Group { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }

    public AIRenovationEstimate Estimate { get; set; } = null!;
}
