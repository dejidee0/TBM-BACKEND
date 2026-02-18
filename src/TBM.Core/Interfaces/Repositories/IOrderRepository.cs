using TBM.Core.Entities.Orders;
using TBM.Core.Enums;
using TBM.Core.DTOs.Admin;

namespace TBM.Core.Interfaces.Repositories;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id);
    Task<Order?> GetByOrderNumberAsync(string orderNumber);
    Task<List<MonthlyRevenueDto>> GetMonthlyRevenueAsync(int months);
Task<List<PaymentDistributionDto>> GetPaymentDistributionAsync();

    Task<decimal> GetRevenueAsync(DateTime from, DateTime to);
Task<int> GetOrderCountAsync(DateTime from, DateTime to);

    Task<(IEnumerable<Order> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Guid? userId = null,
        OrderStatus? status = null,
        PaymentStatus? paymentStatus = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? searchTerm = null
    );
    Task<IEnumerable<Order>> GetUserOrdersAsync(Guid userId);
    Task<Order> CreateAsync(Order order);
    Task UpdateAsync(Order order);
    Task DeleteAsync(Guid id);
    IQueryable<Order> GetQueryable();

    Task<string> GenerateOrderNumberAsync();
    Task<decimal> GetTotalSalesAsync(DateTime? fromDate = null, DateTime? toDate = null);
    Task<int> GetTotalOrdersCountAsync(OrderStatus? status = null);
}