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

public async Task<List<RevenueByServiceDto>> GetRevenueByServiceAsync(DateTime? fromDate = null, DateTime? toDate = null)
{
    var query = _context.OrderItems
        .Where(i =>
            (i.Order.PaymentStatus == PaymentStatus.Paid || i.Order.Status == OrderStatus.Completed) &&
            !i.Order.IsDeleted);

    if (fromDate.HasValue)
    {
        query = query.Where(i => i.Order.CreatedAt >= fromDate.Value);
    }

    if (toDate.HasValue)
    {
        query = query.Where(i => i.Order.CreatedAt <= toDate.Value);
    }

    return await query
        .GroupBy(i => i.Product.ProductType)
        .Select(g => new RevenueByServiceDto
        {
            Service = g.Key.ToString(),
            Revenue = g.Sum(x => x.SubTotal),
            Orders = g.Select(x => x.OrderId).Distinct().Count()
        })
        .OrderByDescending(x => x.Revenue)
        .ToListAsync();
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

    public async Task<Order?> GetByPaymentReferenceAsync(string paymentReference, Guid? userId = null)
    {
        var query = _context.Orders
            .Include(o => o.Items)
            .Include(o => o.User)
            .Where(o => o.PaymentReference == paymentReference);

        if (userId.HasValue)
        {
            query = query.Where(o => o.UserId == userId.Value);
        }

        return await query
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();
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
                (o.User != null && o.User.Email.Contains(searchTerm)) ||
                (o.GuestEmail != null && o.GuestEmail.Contains(searchTerm))
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
        var entry = _context.Entry(order);

        if (entry.State == EntityState.Detached)
        {
            // Attach the caller's own instance and mark it Modified. We
            // deliberately KEEP the entity's RowVersion as the concurrency
            // token's original value, so the generated
            //   UPDATE ... WHERE Id = @id AND RowVersion = @loadedRowVersion
            // still performs a genuine optimistic-concurrency check against the
            // value that was read when the caller loaded the order.
            //
            // We must NOT re-read the current RowVersion from the database here:
            // doing so would make the WHERE clause always match and silently
            // turn this into a last-write-wins update, allowing concurrent
            // writers (e.g. a Paystack webhook racing an admin edit) to clobber
            // each other undetected. A real conflict must surface as a
            // DbUpdateConcurrencyException so the caller's retry loop can reload
            // and reapply against the current row.
            _context.Orders.Attach(order);
            entry = _context.Entry(order);
            entry.State = EntityState.Modified;
        }

        // For an already-tracked entity the change tracker already holds the
        // RowVersion captured when it was loaded; the SaveChangesAsync override
        // stamps UpdatedAt. We set it here too so detached updates carry it.
        entry.Entity.UpdatedAt = DateTime.UtcNow;
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

        // NEXT VALUE FOR is a single atomic operation in SQL Server, so
        // concurrent checkouts can never be handed the same value — unlike
        // the previous read-last-row-then-increment, which raced under
        // concurrent requests and produced duplicate OrderNumber values that
        // failed the unique index at INSERT time.
        var sequenceValue = await _context.Database
            .SqlQueryRaw<long>("SELECT NEXT VALUE FOR OrderNumberSequence")
            .FirstAsync();

        return $"{prefix}{sequenceValue:D4}";
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

    public async Task<int> GetCountByPaymentStatusAsync(PaymentStatus paymentStatus, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _context.Orders
            .Where(o => o.PaymentStatus == paymentStatus);

        if (fromDate.HasValue)
        {
            query = query.Where(o => o.CreatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(o => o.CreatedAt <= toDate.Value);
        }

        return await query.CountAsync();
    }

    public async Task<decimal> GetRevenueByPaymentStatusAsync(PaymentStatus paymentStatus, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _context.Orders
            .Where(o => o.PaymentStatus == paymentStatus);

        if (fromDate.HasValue)
        {
            query = query.Where(o => o.CreatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(o => o.CreatedAt <= toDate.Value);
        }

        return await query.SumAsync(o => (decimal?)o.Total) ?? 0m;
    }
}
