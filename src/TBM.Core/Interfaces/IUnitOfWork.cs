using TBM.Core.Interfaces.Repositories;
using TBM.Core.Interfaces.Repositories.AI;

namespace TBM.Core.Interfaces;

public interface IUnitOfWork : IDisposable
{
    // User repositories
    IUserRepository Users { get; }
    IRoleRepository Roles { get; }

    IAIProjectRepository AIProjects { get; }
    IAIDesignRepository AIDesigns { get; }

ISettingRepository Settings { get; }
IAuditLogRepository AuditLogs { get; }

    
    // Product repositories
    ICategoryRepository Categories { get; }
    IProductRepository Products { get; }
    IProductImageRepository ProductImages { get; }
    
    // Order repositories
    ICartRepository Carts { get; }
    IOrderRepository Orders { get; }      
IOrderStatusHistoryRepository OrderStatusHistories { get; }
IWebhookEventRepository WebhookEvents { get; }


    
    Task<int> SaveChangesAsync();
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}