using Microsoft.EntityFrameworkCore;
using TBM.Core.Entities.Users;
using TBM.Core.Entities.Products;
using TBM.Core.Entities.Orders; // Add this
using TBM.Infrastructure.Data.Configurations;
using TBM.Core.Entities.AI;
using TBM.Core.Entities;
using TBM.Core.Entities.Audit;
using TBM.Core.Entities.Payments;


namespace TBM.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<AIProject> AIProjects => Set<AIProject>();
public DbSet<AIDesign> AIDesigns => Set<AIDesign>();
public DbSet<AuditLog> AuditLogs { get; set; }

public DbSet<AIUsage> AIUsages => Set<AIUsage>();

    
    // User tables
    public DbSet<User> Users { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<UserRole> UserRoles { get; set; }
    public DbSet<UserAddress> UserAddresses { get; set; }
    
    // Product tables
    public DbSet<Category> Categories { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<ProductImage> ProductImages { get; set; }
    
    // Order tables
    public DbSet<Cart> Carts { get; set; }
    public DbSet<CartItem> CartItems { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    
    public DbSet<Setting> Settings { get; set; }
    public DbSet<OrderStatusHistory> OrderStatusHistories { get; set; }
public DbSet<WebhookEvent> WebhookEvents { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // User configurations
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new RoleConfiguration());
        modelBuilder.ApplyConfiguration(new UserRoleConfiguration());
        modelBuilder.ApplyConfiguration(new UserAddressConfiguration());
        
        // Product configurations
        modelBuilder.ApplyConfiguration(new CategoryConfiguration());
        modelBuilder.ApplyConfiguration(new ProductConfiguration());
        modelBuilder.ApplyConfiguration(new ProductImageConfiguration());

        modelBuilder.Entity<User>()
    .HasQueryFilter(u => !u.IsDeleted);

        
        // Order configurations
        modelBuilder.ApplyConfiguration(new CartConfiguration());
        modelBuilder.ApplyConfiguration(new CartItemConfiguration());
        modelBuilder.ApplyConfiguration(new OrderConfiguration());
        modelBuilder.ApplyConfiguration(new OrderItemConfiguration());
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

    }
    
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is TBM.Core.Entities.Common.AuditableEntity && 
                       (e.State == EntityState.Added || e.State == EntityState.Modified));
        
        foreach (var entry in entries)
        {
            var entity = (TBM.Core.Entities.Common.AuditableEntity)entry.Entity;
            
            if (entry.State == EntityState.Added)
            {
                entity.CreatedAt = DateTime.UtcNow;
            }
            
            entity.UpdatedAt = DateTime.UtcNow;
        }
        
        return base.SaveChangesAsync(cancellationToken);
    }
}