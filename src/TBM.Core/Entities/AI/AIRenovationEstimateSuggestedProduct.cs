using TBM.Core.Entities.Common;

namespace TBM.Core.Entities.AI;

public class AIRenovationEstimateSuggestedProduct : AuditableEntity
{
    public Guid EstimateId { get; set; }
    public Guid? ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public string? Category { get; set; }
    public string Link { get; set; } = string.Empty;

    public AIRenovationEstimate Estimate { get; set; } = null!;
}
