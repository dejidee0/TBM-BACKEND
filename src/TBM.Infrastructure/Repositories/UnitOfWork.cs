using Microsoft.EntityFrameworkCore.Storage;
using TBM.Core.Interfaces;
using TBM.Core.Interfaces.Repositories;
using TBM.Core.Interfaces.Repositories.AI;
using TBM.Infrastructure.Data;

namespace TBM.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private IDbContextTransaction? _transaction;

    // User repositories
    public IUserRepository Users { get; }
    public IRoleRepository Roles { get; }

    public IAIProjectRepository AIProjects { get; }
    public IAIDesignRepository AIDesigns { get; }
    public ISettingRepository Settings { get; }
    public IOrderStatusHistoryRepository OrderStatusHistories { get; }

    // Product repositories
    public ICategoryRepository Categories { get; }
    public IProductRepository Products { get; }
    public IProductImageRepository ProductImages { get; }

    // Order repositories
    public ICartRepository Carts { get; }
    public IWebhookEventRepository WebhookEvents { get; }
    public IOrderRepository Orders { get; }
    public IAuditLogRepository AuditLogs { get; }

    public UnitOfWork(
        ApplicationDbContext context,
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IAuditLogRepository auditLogs,
        ISettingRepository settingRepository,
        IOrderStatusHistoryRepository orderStatusHistories,
        ICategoryRepository categoryRepository,
        IProductRepository productRepository,
        IProductImageRepository productImageRepository,
        ICartRepository cartRepository,
        IAIProjectRepository aiProjects,
        IAIDesignRepository aiDesigns,
        IWebhookEventRepository webhookEvents,
        IOrderRepository orderRepository)
    {
        _context = context;
        Users = userRepository;
        Roles = roleRepository;
        AuditLogs = auditLogs;
        Categories = categoryRepository;
        OrderStatusHistories = orderStatusHistories;

        Settings = settingRepository;
        Products = productRepository;
        ProductImages = productImageRepository;
        Carts = cartRepository;
        AIProjects = aiProjects;
        AIDesigns = aiDesigns;
        Orders = orderRepository;
        WebhookEvents = webhookEvents;
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
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

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}