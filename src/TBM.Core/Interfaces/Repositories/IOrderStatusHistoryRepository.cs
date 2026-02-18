using TBM.Core.Entities.Orders;

namespace TBM.Core.Interfaces.Repositories;

public interface IOrderStatusHistoryRepository
{
    Task AddAsync(OrderStatusHistory history);
}
