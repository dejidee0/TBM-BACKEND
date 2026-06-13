using TBM.Core.DTOs.Admin;
using TBM.Application.DTOs.Admin;
using TBM.Core.Interfaces;

namespace TBM.Application.Services;

public class AdminAnalyticsService
{
    private readonly IUnitOfWork _unitOfWork;

    public AdminAnalyticsService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<AdminAnalyticsOverviewDto> GetOverviewAsync()
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

        return new AdminAnalyticsOverviewDto
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
    return await _unitOfWork.Orders.GetMonthlyRevenueAsync(12);
}

public async Task<List<TBM.Core.DTOs.Admin.PaymentDistributionDto>> GetPaymentDistributionAsync()
{
    return await _unitOfWork.Orders.GetPaymentDistributionAsync();
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
