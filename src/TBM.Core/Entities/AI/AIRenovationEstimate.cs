using TBM.Core.Entities.Common;

namespace TBM.Core.Entities.AI;

public class AIRenovationEstimate : AuditableEntity
{
    public Guid UserId { get; set; }
    public string EstimateNumber { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string RoomType { get; set; } = string.Empty;
    public decimal LengthMeters { get; set; }
    public decimal WidthMeters { get; set; }
    public decimal HeightMeters { get; set; }
    public string FinishLevel { get; set; } = "standard";
    public bool IncludeFlooring { get; set; }
    public bool IncludePainting { get; set; }
    public bool IncludeElectrical { get; set; }
    public bool IncludePlumbing { get; set; }
    public decimal ContingencyPercent { get; set; }
    public decimal FloorAreaSqm { get; set; }
    public decimal WallAreaSqm { get; set; }
    public string Currency { get; set; } = "NGN";
    public decimal MaterialsSubtotal { get; set; }
    public decimal LaborSubtotal { get; set; }
    public decimal ContingencyAmount { get; set; }
    public decimal TotalEstimate { get; set; }
    public string Summary { get; set; } = string.Empty;

    public ICollection<AIRenovationEstimateLineItem> LineItems { get; set; } = new List<AIRenovationEstimateLineItem>();
    public ICollection<AIRenovationEstimateSuggestedProduct> SuggestedProducts { get; set; } = new List<AIRenovationEstimateSuggestedProduct>();
}
