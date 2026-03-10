namespace TBM.Application.DTOs.Admin;

public class AdminDashboardStatsDto
{
    public string PlatformUptime { get; set; } = string.Empty;
    public int ActiveUsers { get; set; }
    public decimal AvgApiLatency { get; set; }
}

public class AdminDashboardRevenueDto
{
    public string TimeRange { get; set; } = "30d";
    public decimal TotalRevenue { get; set; }
    public decimal MonthlyRecurring { get; set; }
    public List<AdminRevenueChartPointDto> ChartData { get; set; } = new();
}

public class AdminRevenueChartPointDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Revenue { get; set; }
}

public class AdminDashboardServerLoadDto
{
    public string Cluster { get; set; } = "primary";
    public string Capacity { get; set; } = "healthy";
    public string Status { get; set; } = "online";
    public decimal CpuUsage { get; set; }
    public decimal MemoryUsage { get; set; }
    public decimal DiskUsage { get; set; }
}

public class AdminDashboardAlertDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Severity { get; set; } = "info";
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AdminQuickActionDto
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string Method { get; set; } = "GET";
}
