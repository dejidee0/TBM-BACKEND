using System.Diagnostics;
using System.Text;
using TBM.Application.DTOs.Admin;
using TBM.Application.Helpers;
using TBM.Core.Enums;
using TBM.Core.Interfaces;

namespace TBM.Application.Services;

public class AdminDashboardService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly AuditService _audit;

    public AdminDashboardService(IUnitOfWork unitOfWork, AuditService audit)
    {
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    public Task<AdminDashboardStatsDto> GetStatsAsync()
    {
        var uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
        var activeUsers = _unitOfWork.Users.GetQueryable()
            .Count(x => x.IsActive && x.Status == UserStatus.Active && !x.IsDeleted);

        var stats = new AdminDashboardStatsDto
        {
            PlatformUptime = $"{uptime.Days}d {uptime.Hours}h",
            ActiveUsers = activeUsers,
            AvgApiLatency = EstimateApiLatencyMs()
        };

        return Task.FromResult(stats);
    }

    public async Task<AdminDashboardRevenueDto> GetRevenueAsync(string? timeRange)
    {
        var now = DateTime.UtcNow;
        var fromUtc = ResolveFromUtc(timeRange, now);

        var totalRevenue = await _unitOfWork.Orders.GetTotalSalesAsync(fromUtc, now);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthlyRecurring = await _unitOfWork.Orders.GetTotalSalesAsync(monthStart, now);

        var startMonth = new DateTime(fromUtc.Year, fromUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endMonth = monthStart;
        var monthsWindow = DateRangeHelper.GetMonthSpanInclusive(startMonth, endMonth);
        monthsWindow = Math.Clamp(monthsWindow, 1, 24);
        if (monthsWindow < DateRangeHelper.GetMonthSpanInclusive(startMonth, endMonth))
        {
            startMonth = endMonth.AddMonths(-(monthsWindow - 1));
        }

        var series = await _unitOfWork.Orders.GetMonthlyRevenueAsync(monthsWindow);
        var lookup = series.ToDictionary(x => (x.Year, x.Month), x => x.Revenue);
        var chartData = DateRangeHelper.GetMonthStartsUtc(startMonth, endMonth)
            .Select(m => new AdminRevenueChartPointDto
            {
                Year = m.Year,
                Month = m.Month,
                Revenue = lookup.TryGetValue((m.Year, m.Month), out var revenue) ? revenue : 0m
            })
            .ToList();

        return new AdminDashboardRevenueDto
        {
            TimeRange = string.IsNullOrWhiteSpace(timeRange) ? "30d" : timeRange.Trim(),
            TotalRevenue = totalRevenue,
            MonthlyRecurring = monthlyRecurring,
            ChartData = chartData
        };
    }

    public Task<AdminDashboardServerLoadDto> GetServerLoadAsync()
    {
        var process = Process.GetCurrentProcess();
        var uptimeSeconds = Math.Max(1, (DateTime.UtcNow - process.StartTime.ToUniversalTime()).TotalSeconds);

        var avgCpu = (decimal)(process.TotalProcessorTime.TotalSeconds / (uptimeSeconds * Environment.ProcessorCount) * 100d);
        avgCpu = Clamp(avgCpu);

        var gcInfo = GC.GetGCMemoryInfo();
        var totalAvailable = gcInfo.TotalAvailableMemoryBytes <= 0 ? 1 : gcInfo.TotalAvailableMemoryBytes;
        var memoryUsage = (decimal)(GC.GetTotalMemory(false) * 100d / totalAvailable);
        memoryUsage = Clamp(memoryUsage);

        var diskUsage = GetDiskUsagePercentage();

        var dto = new AdminDashboardServerLoadDto
        {
            Cluster = "primary",
            Capacity = avgCpu > 90 || memoryUsage > 90 || diskUsage > 90 ? "critical" : "healthy",
            Status = avgCpu > 95 || memoryUsage > 95 || diskUsage > 95 ? "degraded" : "online",
            CpuUsage = Math.Round(avgCpu, 2),
            MemoryUsage = Math.Round(memoryUsage, 2),
            DiskUsage = Math.Round(diskUsage, 2)
        };

        return Task.FromResult(dto);
    }

    public async Task<List<AdminDashboardAlertDto>> GetAlertsAsync()
    {
        var alerts = new List<AdminDashboardAlertDto>();

        var pendingOrders = await _unitOfWork.Orders.GetTotalOrdersCountAsync(OrderStatus.Pending);
        if (pendingOrders > 20)
        {
            alerts.Add(new AdminDashboardAlertDto
            {
                Severity = "warning",
                Message = $"Pending orders are high ({pendingOrders})."
            });
        }

        var refundedOrders = await _unitOfWork.Orders.GetTotalOrdersCountAsync(OrderStatus.Refunded);
        if (refundedOrders > 0)
        {
            alerts.Add(new AdminDashboardAlertDto
            {
                Severity = "info",
                Message = $"There are {refundedOrders} refunded orders to review."
            });
        }

        if (!alerts.Any())
        {
            alerts.Add(new AdminDashboardAlertDto
            {
                Severity = "info",
                Message = "No operational alerts right now."
            });
        }

        return alerts
            .OrderByDescending(x => x.CreatedAt)
            .ToList();
    }

    public Task<List<AdminQuickActionDto>> GetQuickActionsAsync()
    {
        var actions = new List<AdminQuickActionDto>
        {
            new() { Id = "users", Label = "Manage Users", Route = "/api/admin/users", Method = "GET" },
            new() { Id = "orders", Label = "Review Orders", Route = "/api/admin/orders", Method = "GET" },
            new() { Id = "financial", Label = "Financial Export", Route = "/api/admin/financial/export", Method = "GET" },
            new() { Id = "logs", Label = "System Logs", Route = "/api/admin/system-logs", Method = "GET" }
        };

        return Task.FromResult(actions);
    }

    public Task<object> RefreshAsync()
    {
        return Task.FromResult<object>(new
        {
            success = true,
            message = "Dashboard refreshed successfully.",
            refreshedAt = DateTime.UtcNow
        });
    }

    public async Task<(string FileName, string ContentType, byte[] Content)> ExportAsync()
    {
        var stats = await GetStatsAsync();
        var revenue = await GetRevenueAsync("90d");
        var serverLoad = await GetServerLoadAsync();

        var csv = new StringBuilder();
        csv.AppendLine("section,key,value");
        csv.AppendLine($"stats,platformUptime,\"{EscapeCsv(stats.PlatformUptime)}\"");
        csv.AppendLine($"stats,activeUsers,{stats.ActiveUsers}");
        csv.AppendLine($"stats,avgApiLatency,{stats.AvgApiLatency}");
        csv.AppendLine($"serverLoad,cpuUsage,{serverLoad.CpuUsage}");
        csv.AppendLine($"serverLoad,memoryUsage,{serverLoad.MemoryUsage}");
        csv.AppendLine($"serverLoad,diskUsage,{serverLoad.DiskUsage}");
        csv.AppendLine($"revenue,timeRange,\"{EscapeCsv(revenue.TimeRange)}\"");
        csv.AppendLine($"revenue,totalRevenue,{revenue.TotalRevenue}");
        csv.AppendLine($"revenue,monthlyRecurring,{revenue.MonthlyRecurring}");

        foreach (var point in revenue.ChartData)
        {
            csv.AppendLine($"revenueChart,{point.Year}-{point.Month:D2},{point.Revenue}");
        }

        var fileName = $"admin-dashboard-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        var content = Encoding.UTF8.GetBytes(csv.ToString());

        await _audit.LogAsync(
            action: "AdminExport",
            category: "Dashboard",
            oldValue: null,
            newValue: new { fileName, rows = revenue.ChartData.Count + 9 });

        return (fileName, "text/csv", content);
    }

    private static DateTime ResolveFromUtc(string? timeRange, DateTime now)
    {
        var normalized = (timeRange ?? "30d").Trim().ToLowerInvariant();
        return normalized switch
        {
            "7d" => now.AddDays(-7),
            "30d" => now.AddDays(-30),
            "90d" => now.AddDays(-90),
            "6m" => now.AddMonths(-6),
            "12m" => now.AddMonths(-12),
            _ => now.AddDays(-30)
        };
    }

    private static decimal EstimateApiLatencyMs()
    {
        var process = Process.GetCurrentProcess();
        var threadCount = process.Threads.Count;
        var memoryMb = GC.GetTotalMemory(false) / (1024d * 1024d);
        var estimated = 40m + (threadCount * 0.6m) + (decimal)Math.Min(memoryMb / 50d, 60d);
        return Math.Round(estimated, 2);
    }

    private static decimal GetDiskUsagePercentage()
    {
        try
        {
            var root = Path.GetPathRoot(AppContext.BaseDirectory);
            if (string.IsNullOrWhiteSpace(root))
            {
                return 0m;
            }

            var drive = new DriveInfo(root);
            if (!drive.IsReady || drive.TotalSize <= 0)
            {
                return 0m;
            }

            var used = drive.TotalSize - drive.AvailableFreeSpace;
            return Clamp((decimal)(used * 100d / drive.TotalSize));
        }
        catch
        {
            return 0m;
        }
    }

    private static decimal Clamp(decimal value)
    {
        if (value < 0m) return 0m;
        if (value > 100m) return 100m;
        return value;
    }

    private static string EscapeCsv(string value)
    {
        return value.Replace("\"", "\"\"");
    }
}
