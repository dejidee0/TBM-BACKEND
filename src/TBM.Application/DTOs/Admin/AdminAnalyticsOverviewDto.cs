namespace TBM.Application.DTOs.Admin;

public class AdminAnalyticsOverviewDto
{
    public decimal TotalRevenue { get; set; }
    public decimal RevenueGrowthPercentage { get; set; }

    public int TotalOrders { get; set; }
    public decimal OrdersGrowthPercentage { get; set; }

    public int TotalUsers { get; set; }
    public decimal UsersGrowthPercentage { get; set; }

    public decimal AverageOrderValue { get; set; }
}
