using TBM.Core.Interfaces.Repositories;
using TBM.Core.Interfaces.Repositories.AI;
using TBM.Core.Interfaces.Repositories.DesignFlow;
using TBM.Core.Interfaces.Repositories.Subscriptions;

namespace TBM.Core.Interfaces;

public interface IUnitOfWork : IDisposable
{
    // User repositories
    IUserRepository Users { get; }
    IUserAddressRepository UserAddresses { get; }
    IRoleRepository Roles { get; }

    IAIProjectRepository AIProjects { get; }
    IAIDesignRepository AIDesigns { get; }
    IAIUsageRepository AIUsages { get; }
    IAIRenovationEstimateRepository AIRenovationEstimates { get; }
    IAIAssistantRepository AIAssistant { get; }

    IDesignSessionRepository DesignSessions { get; }
    IBillOfMaterialsRepository BillsOfMaterials { get; }
    IProjectRepository Projects { get; }

    ISettingRepository Settings { get; }
    IAuditLogRepository AuditLogs { get; }

    
    // Product repositories
    ICategoryRepository Categories { get; }
    IProductRepository Products { get; }
    IProductImageRepository ProductImages { get; }
    IProductVariantRepository ProductVariants { get; }
    
    // Order repositories
    ICartRepository Carts { get; }
    IOrderRepository Orders { get; }      
    IOrderStatusHistoryRepository OrderStatusHistories { get; }
    IWebhookEventRepository WebhookEvents { get; }

    // Subscription repositories
    ISubscriptionRepository Subscriptions { get; }
    IPricingConfigRepository PricingConfigs { get; }
    IDiscountRepository Discounts { get; }

    // Portfolio
    IPortfolioRepository Portfolio { get; }

    Task<int> SaveChangesAsync();
    void ClearChangeTracker();
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
    Task ExecuteInTransactionAsync(Func<Task> operation);
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation);
}
