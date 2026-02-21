namespace TBM.Core.Models.AI;

public class AIUsageSummary
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public int TotalGenerations { get; set; }
    public int TotalCreditsUsed { get; set; }
    public decimal TotalEstimatedCost { get; set; }
}
