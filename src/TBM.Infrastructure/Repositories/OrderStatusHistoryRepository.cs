using TBM.Core.Entities.Orders;
using TBM.Core.Interfaces.Repositories;
using TBM.Infrastructure.Data;

namespace TBM.Infrastructure.Repositories;

public class OrderStatusHistoryRepository : IOrderStatusHistoryRepository
{
    private readonly ApplicationDbContext _context;

    public OrderStatusHistoryRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(OrderStatusHistory history)
    {
        await _context.OrderStatusHistories.AddAsync(history);
    }
}
