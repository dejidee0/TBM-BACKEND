namespace TBM.Core.Models.AI;

public class AIUsageMonthlySpend
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int TotalGenerations { get; set; }
    public int TotalCreditsUsed { get; set; }
    public decimal TotalEstimatedCost { get; set; }
    public int DistinctUsers { get; set; }
}
