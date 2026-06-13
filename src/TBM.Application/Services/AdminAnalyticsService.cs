using TBM.Core.DTOs.Admin;
using TBM.Application.Helpers;
using TBM.Core.Enums;
using TBM.Core.Interfaces;

namespace TBM.Application.Services;

public class AdminAnalyticsService
{
    private readonly IUnitOfWork _unitOfWork;

    public AdminAnalyticsService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<TBM.Application.DTOs.Admin.AdminAnalyticsOverviewDto> GetOverviewAsync()
    {
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1);
        var startOfLastMonth = startOfMonth.AddMonths(-1);

        var currentRevenue = await _unitOfWork.Orders
            .GetRevenueAsync(startOfMonth, now);

        var lastRevenue = await _unitOfWork.Orders
            .GetRevenueAsync(startOfLastMonth, startOfMonth);

        var currentOrders = await _unitOfWork.Orders
            .GetOrderCountAsync(startOfMonth, now);

        var lastOrders = await _unitOfWork.Orders
            .GetOrderCountAsync(startOfLastMonth, startOfMonth);

        var currentUsers = await _unitOfWork.Users
            .GetUserCountAsync(startOfMonth, now);

        var lastUsers = await _unitOfWork.Users
            .GetUserCountAsync(startOfLastMonth, startOfMonth);

        return new TBM.Application.DTOs.Admin.AdminAnalyticsOverviewDto
        {
            TotalRevenue = currentRevenue,
            RevenueGrowthPercentage = CalculateGrowth(lastRevenue, currentRevenue),

            TotalOrders = currentOrders,
            OrdersGrowthPercentage = CalculateGrowth(lastOrders, currentOrders),

            TotalUsers = currentUsers,
            UsersGrowthPercentage = CalculateGrowth(lastUsers, currentUsers),

            AverageOrderValue = currentOrders == 0 ? 0 : currentRevenue / currentOrders
        };
    }

public async Task<List<TBM.Core.DTOs.Admin.MonthlyRevenueDto>> GetMonthlyRevenueAsync()
{
    const int months = 12;
    var rows = await _unitOfWork.Orders.GetMonthlyRevenueAsync(months);

    var now = DateTime.UtcNow;
    var endMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    var startMonth = endMonth.AddMonths(-(months - 1));

    var lookup = rows.ToDictionary(x => (x.Year, x.Month), x => x.Revenue);
    return DateRangeHelper.GetMonthStartsUtc(startMonth, endMonth)
        .Select(m => new TBM.Core.DTOs.Admin.MonthlyRevenueDto
        {
            Year = m.Year,
            Month = m.Month,
            Revenue = lookup.TryGetValue((m.Year, m.Month), out var revenue) ? revenue : 0m
        })
        .ToList();
}

public async Task<List<TBM.Core.DTOs.Admin.PaymentDistributionDto>> GetPaymentDistributionAsync()
{
    var rows = await _unitOfWork.Orders.GetPaymentDistributionAsync();
    var enumNames = Enum.GetNames(typeof(PaymentMethod));
    var expected = new HashSet<string>(enumNames, StringComparer.OrdinalIgnoreCase)
    {
        "Unknown"
    };

    var map = rows.ToDictionary(x => x.PaymentMethod, StringComparer.OrdinalIgnoreCase);
    var merged = new List<TBM.Core.DTOs.Admin.PaymentDistributionDto>();

    foreach (var name in enumNames)
    {
        if (map.TryGetValue(name, out var row))
        {
            merged.Add(new TBM.Core.DTOs.Admin.PaymentDistributionDto
            {
                PaymentMethod = string.IsNullOrWhiteSpace(row.PaymentMethod) ? name : row.PaymentMethod,
                Revenue = row.Revenue
            });
        }
        else
        {
            merged.Add(new TBM.Core.DTOs.Admin.PaymentDistributionDto
            {
                PaymentMethod = name,
                Revenue = 0m
            });
        }
    }

    if (map.TryGetValue("Unknown", out var unknown))
    {
        merged.Add(new TBM.Core.DTOs.Admin.PaymentDistributionDto
        {
            PaymentMethod = "Unknown",
            Revenue = unknown.Revenue
        });
    }
    else
    {
        merged.Add(new TBM.Core.DTOs.Admin.PaymentDistributionDto
        {
            PaymentMethod = "Unknown",
            Revenue = 0m
        });
    }

    foreach (var row in rows)
    {
        if (!expected.Contains(row.PaymentMethod))
        {
            merged.Add(row);
        }
    }

    return merged
        .OrderByDescending(x => x.Revenue)
        .ThenBy(x => x.PaymentMethod)
        .ToList();
}

    private decimal CalculateGrowth(decimal previous, decimal current)
    {
        if (previous == 0) return current > 0 ? 100 : 0;
        return ((current - previous) / previous) * 100;
    }

    private decimal CalculateGrowth(int previous, int current)
    {
        if (previous == 0) return current > 0 ? 100 : 0;
        return ((decimal)(current - previous) / previous) * 100;
    }
}
