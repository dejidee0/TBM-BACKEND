using Microsoft.EntityFrameworkCore;
using TBM.Core.Entities.Users;
using TBM.Core.Entities.Products;
using TBM.Core.Entities.Orders; // Add this
using TBM.Infrastructure.Data.Configurations;
using TBM.Core.Entities.AI;
using TBM.Core.Entities;
using TBM.Core.Entities.Audit;
using TBM.Core.Entities.Contact;
using TBM.Core.Entities.Payments;
using TBM.Core.Entities.DesignFlow;
using TBM.Core.Entities.Subscriptions;
using TBM.Core.Entities.Portfolio;
using TBM.Core.Entities.Inspections;
using TBM.Core.Entities.ProjectRequests;
using TBM.Core.Entities.Inspiration;


namespace TBM.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<AIProject> AIProjects => Set<AIProject>();
public DbSet<AIDesign> AIDesigns => Set<AIDesign>();
public DbSet<AIRenovationEstimate> AIRenovationEstimates => Set<AIRenovationEstimate>();
public DbSet<AIRenovationEstimateLineItem> AIRenovationEstimateLineItems => Set<AIRenovationEstimateLineItem>();
public DbSet<AIRenovationEstimateSuggestedProduct> AIRenovationEstimateSuggestedProducts => Set<AIRenovationEstimateSuggestedProduct>();
public DbSet<AIAssistantSession> AIAssistantSessions => Set<AIAssistantSession>();
public DbSet<AIAssistantMessage> AIAssistantMessages => Set<AIAssistantMessage>();
public DbSet<AIAssistantTask> AIAssistantTasks => Set<AIAssistantTask>();
public DbSet<AIAssistantToolAction> AIAssistantToolActions => Set<AIAssistantToolAction>();
public DbSet<AIAssistantToolApproval> AIAssistantToolApprovals => Set<AIAssistantToolApproval>();
public DbSet<AIAssistantToolExecution> AIAssistantToolExecutions => Set<AIAssistantToolExecution>();
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
    public DbSet<ProductVariant> ProductVariants { get; set; }
    
    // Order tables
    public DbSet<Cart> Carts { get; set; }
    public DbSet<CartItem> CartItems { get; set; }
public DbSet<Order> Orders { get; set; }
public DbSet<OrderItem> OrderItems { get; set; }
public DbSet<DesignSession> DesignSessions { get; set; }
public DbSet<BillOfMaterials> BillsOfMaterials { get; set; }
public DbSet<BOMItem> BOMItems { get; set; }
public DbSet<Project> Projects { get; set; }
public DbSet<ProjectTimeline> ProjectTimelines { get; set; }
public DbSet<ProjectDocument> ProjectDocuments { get; set; }
public DbSet<SiteGalleryImage> SiteGalleryImages { get; set; }
    
    public DbSet<Setting> Settings { get; set; }
    public DbSet<OrderStatusHistory> OrderStatusHistories { get; set; }
public DbSet<WebhookEvent> WebhookEvents { get; set; }
public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();

    // Subscription tables
    public DbSet<Subscription> Subscriptions { get; set; }
    public DbSet<PricingConfig> PricingConfigs { get; set; }
    public DbSet<Discount> Discounts { get; set; }

    // Vendor Portfolio
    public DbSet<VendorPortfolioProject> VendorPortfolioProjects { get; set; }
    public DbSet<PortfolioProjectImage> PortfolioProjectImages { get; set; }

    // Mobile feature entities
    public DbSet<InspectionRequest> InspectionRequests => Set<InspectionRequest>();
    public DbSet<ProjectRequest> ProjectRequests => Set<ProjectRequest>();
    public DbSet<InspirationDesign> InspirationDesigns => Set<InspirationDesign>();


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

        // Backs order-number generation with a DB-level atomic counter instead
        // of a read-last-row-then-increment, which raced under concurrent
        // checkouts and produced duplicate OrderNumber values.
        modelBuilder.HasSequence<int>("OrderNumberSequence").StartsAt(1).IncrementsBy(1);
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
