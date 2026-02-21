namespace TBM.Application.DTOs.AI;

public class AIUsageSummaryResponseDto
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public int TotalGenerations { get; set; }
    public int TotalCreditsUsed { get; set; }
    public decimal TotalEstimatedCost { get; set; }
}

public class AIUsageMonthlySpendResponseDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int TotalGenerations { get; set; }
    public int TotalCreditsUsed { get; set; }
    public decimal TotalEstimatedCost { get; set; }
    public int DistinctUsers { get; set; }
}
