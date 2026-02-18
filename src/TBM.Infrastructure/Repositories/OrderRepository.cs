using Microsoft.EntityFrameworkCore;
using TBM.Core.DTOs.Admin;
using TBM.Core.Entities.Orders;
using TBM.Core.Enums;
using TBM.Core.Interfaces.Repositories;
using TBM.Infrastructure.Data;

namespace TBM.Infrastructure.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly ApplicationDbContext _context;
    
    public OrderRepository(ApplicationDbContext context)
    {
        _context = context;
    }
    
    public IQueryable<Order> GetQueryable()
{
    return _context.Orders.AsQueryable();
}

public async Task<decimal> GetRevenueAsync(DateTime from, DateTime to)
{
    return await _context.Orders
        .Where(o => o.Status == OrderStatus.Completed &&
                    o.CreatedAt >= from &&
                    o.CreatedAt < to)
        .SumAsync(o => (decimal?)o.Total) ?? 0;
}

public async Task<int> GetOrderCountAsync(DateTime from, DateTime to)
{
    return await _context.Orders
        .Where(o => o.CreatedAt >= from &&
                    o.CreatedAt < to)
        .CountAsync();
}

public async Task<List<MonthlyRevenueDto>> GetMonthlyRevenueAsync(int months)
{
    var startDate = DateTime.UtcNow.AddMonths(-months);

    return await _context.Orders
        .Where(o => o.Status == OrderStatus.Completed &&
                    o.CreatedAt >= startDate)
        .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month })
        .Select(g => new MonthlyRevenueDto
        {
            Year = g.Key.Year,
            Month = g.Key.Month,
            Revenue = g.Sum(x => x.Total)
        })
        .OrderBy(x => x.Year)
        .ThenBy(x => x.Month)
        .ToListAsync();
}

public async Task<List<PaymentDistributionDto>> GetPaymentDistributionAsync()
{
    return await _context.Orders
        .Where(o => o.Status == OrderStatus.Completed)
        .GroupBy(o => o.PaymentMethod)
        .Select(g => new PaymentDistributionDto
        {
            PaymentMethod = g.Key.HasValue ? g.Key.Value.ToString() : "Unknown",
            Revenue = g.Sum(x => x.Total)
        })
        .ToListAsync();
}



    public async Task<Order?> GetByIdAsync(Guid id)
    {
        return await _context.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == id);
    }
    
    public async Task<Order?> GetByOrderNumberAsync(string orderNumber)
    {
        return await _context.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);
    }
    
    public async Task<(IEnumerable<Order> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Guid? userId = null,
        OrderStatus? status = null,
        PaymentStatus? paymentStatus = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? searchTerm = null)
    {
        var query = _context.Orders
            .Include(o => o.Items)
            .Include(o => o.User)
            .AsQueryable();
        
        if (userId.HasValue)
        {
            query = query.Where(o => o.UserId == userId.Value);
        }
        
        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }
        
        if (paymentStatus.HasValue)
        {
            query = query.Where(o => o.PaymentStatus == paymentStatus.Value);
        }
        
        if (fromDate.HasValue)
        {
            query = query.Where(o => o.CreatedAt >= fromDate.Value);
        }
        
        if (toDate.HasValue)
        {
            query = query.Where(o => o.CreatedAt <= toDate.Value);
        }
        
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(o => 
                o.OrderNumber.Contains(searchTerm) ||
                o.ShippingFullName.Contains(searchTerm) ||
                o.ShippingPhone.Contains(searchTerm) ||
                o.User.Email.Contains(searchTerm)
            );
        }
        
        var totalCount = await query.CountAsync();
        
        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        
        return (items, totalCount);
    }
    
    public async Task<IEnumerable<Order>> GetUserOrdersAsync(Guid userId)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }
    
    public async Task<Order> CreateAsync(Order order)
    {
        await _context.Orders.AddAsync(order);
        return order;
    }
    
    public Task UpdateAsync(Order order)
    {
        _context.Orders.Update(order);
        return Task.CompletedTask;
    }
    
    public async Task DeleteAsync(Guid id)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order != null)
        {
            order.IsDeleted = true;
            order.DeletedAt = DateTime.UtcNow;
            _context.Orders.Update(order);
        }
    }
    
    public async Task<string> GenerateOrderNumberAsync()
    {
        var date = DateTime.UtcNow;
        var prefix = $"ORD{date:yyyyMMdd}";
        
        var lastOrder = await _context.Orders
            .Where(o => o.OrderNumber.StartsWith(prefix))
            .OrderByDescending(o => o.OrderNumber)
            .FirstOrDefaultAsync();
        
        if (lastOrder == null)
        {
            return $"{prefix}0001";
        }
        
        var lastNumber = int.Parse(lastOrder.OrderNumber.Substring(prefix.Length));
        var newNumber = lastNumber + 1;
        
        return $"{prefix}{newNumber:D4}";
    }
    
    public async Task<decimal> GetTotalSalesAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _context.Orders
            .Where(o => o.PaymentStatus == PaymentStatus.Paid);
        
        if (fromDate.HasValue)
        {
            query = query.Where(o => o.CreatedAt >= fromDate.Value);
        }
        
        if (toDate.HasValue)
        {
            query = query.Where(o => o.CreatedAt <= toDate.Value);
        }
        
        return await query.SumAsync(o => o.Total);
    }
    
    public async Task<int> GetTotalOrdersCountAsync(OrderStatus? status = null)
    {
        var query = _context.Orders.AsQueryable();
        
        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }
        
        return await query.CountAsync();
    }
}