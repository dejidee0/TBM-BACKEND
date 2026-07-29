using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using TBM.Core.Interfaces;
using TBM.Core.Interfaces.Repositories;
using TBM.Core.Interfaces.Repositories.AI;
using TBM.Core.Interfaces.Repositories.DesignFlow;
using TBM.Core.Interfaces.Repositories.Subscriptions;
using TBM.Infrastructure.Data;

namespace TBM.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private IDbContextTransaction? _transaction;

    // User repositories
    public IUserRepository Users { get; }
    public IUserAddressRepository UserAddresses { get; }
    public IRoleRepository Roles { get; }

    public IAIProjectRepository AIProjects { get; }
    public IAIDesignRepository AIDesigns { get; }
    public IAIUsageRepository AIUsages { get; }
    public IAIRenovationEstimateRepository AIRenovationEstimates { get; }
    public IAIAssistantRepository AIAssistant { get; }
    public IDesignSessionRepository DesignSessions { get; }
    public IBillOfMaterialsRepository BillsOfMaterials { get; }
    public IProjectRepository Projects { get; }
    public ISettingRepository Settings { get; }
    public IOrderStatusHistoryRepository OrderStatusHistories { get; }

    // Product repositories
    public ICategoryRepository Categories { get; }
    public IProductRepository Products { get; }
    public IProductImageRepository ProductImages { get; }
    public IProductVariantRepository ProductVariants { get; }

    // Order repositories
    public ICartRepository Carts { get; }
    public IWebhookEventRepository WebhookEvents { get; }
    public IOrderRepository Orders { get; }
    public IAuditLogRepository AuditLogs { get; }

    // Subscription repositories
    public ISubscriptionRepository Subscriptions { get; }
    public IPricingConfigRepository PricingConfigs { get; }
    public IDiscountRepository Discounts { get; }

    // Portfolio
    public IPortfolioRepository Portfolio { get; }

    public UnitOfWork(
        ApplicationDbContext context,
        IUserRepository userRepository,
        IUserAddressRepository userAddressRepository,
        IRoleRepository roleRepository,
        IAuditLogRepository auditLogs,
        ISettingRepository settingRepository,
        IOrderStatusHistoryRepository orderStatusHistories,
        ICategoryRepository categoryRepository,
        IProductRepository productRepository,
        IProductImageRepository productImageRepository,
        IProductVariantRepository productVariantRepository,
        ICartRepository cartRepository,
        IAIProjectRepository aiProjects,
        IAIDesignRepository aiDesigns,
        IAIUsageRepository aiUsages,
        IAIRenovationEstimateRepository aiRenovationEstimates,
        IAIAssistantRepository aiAssistant,
        IDesignSessionRepository designSessions,
        IBillOfMaterialsRepository billsOfMaterials,
        IProjectRepository projects,
        IWebhookEventRepository webhookEvents,
        IOrderRepository orderRepository,
        ISubscriptionRepository subscriptions,
        IPricingConfigRepository pricingConfigs,
        IDiscountRepository discounts,
        IPortfolioRepository portfolio)
    {
        _context = context;
        Users = userRepository;
        UserAddresses = userAddressRepository;
        Roles = roleRepository;
        AuditLogs = auditLogs;
        Categories = categoryRepository;
        OrderStatusHistories = orderStatusHistories;

        Settings = settingRepository;
        Products = productRepository;
        ProductImages = productImageRepository;
        ProductVariants = productVariantRepository;
        Carts = cartRepository;
        AIProjects = aiProjects;
        AIDesigns = aiDesigns;
        AIUsages = aiUsages;
        AIRenovationEstimates = aiRenovationEstimates;
        AIAssistant = aiAssistant;
        DesignSessions = designSessions;
        BillsOfMaterials = billsOfMaterials;
        Projects = projects;
        Orders = orderRepository;
        WebhookEvents = webhookEvents;
        Subscriptions = subscriptions;
        PricingConfigs = pricingConfigs;
        Discounts = discounts;
        Portfolio = portfolio;
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public void ClearChangeTracker()
    {
        _context.ChangeTracker.Clear();
    }

    public async Task BeginTransactionAsync()
    {
        _transaction = await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync()
    {
        try
        {
            await SaveChangesAsync();
            if (_transaction != null)
            {
                await _transaction.CommitAsync();
            }
        }
        catch
        {
            await RollbackTransactionAsync();
            throw;
        }
        finally
        {
            if (_transaction != null)
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public Task ExecuteInTransactionAsync(Func<Task> operation)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await operation();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    public Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var result = await operation();
                await transaction.CommitAsync();
                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}
