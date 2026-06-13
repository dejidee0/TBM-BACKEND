using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TBM.Application.DTOs.Orders;
using TBM.Application.Services;
using TBM.Core.Entities.Orders;
using TBM.Core.Entities.Products;
using TBM.Core.Enums;
using TBM.Core.Interfaces;
using TBM.Core.Interfaces.Repositories;
using TBM.Core.Interfaces.Repositories.AI;
using TBM.Core.Interfaces.Repositories.DesignFlow;
using TBM.Core.Interfaces.Repositories.Subscriptions;
using TBM.Infrastructure.Data;
using TBM.Infrastructure.Repositories;

namespace TBM.API.ContractTests;

public sealed class CartMergeServiceTests
{
    [Fact]
    public async Task MergeGuestCartAsync_DuplicateItems_AreSummed()
    {
        var userId = Guid.NewGuid();
        await using var context = BuildContext(nameof(MergeGuestCartAsync_DuplicateItems_AreSummed));
        var product = await SeedProductAsync(context, "Product A", 40, true, true);
        await SeedCartItemAsync(context, userId, product.Id, 1);

        var service = BuildService(context);

        var result = await service.MergeGuestCartAsync(userId, new MergeCartRequestDto
        {
            Items =
            [
                new MergeCartItemDto { ProductId = product.Id, Quantity = 2 },
                new MergeCartItemDto { ProductId = product.Id, Quantity = 3 }
            ]
        });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var merged = Assert.Single(result.Data!.Cart.Items);
        Assert.Equal(6, merged.Quantity);
        Assert.Empty(result.Data.Warnings);
    }

    [Fact]
    public async Task MergeGuestCartAsync_StockCap_AddsWarning_AndCapsQuantity()
    {
        var userId = Guid.NewGuid();
        await using var context = BuildContext(nameof(MergeGuestCartAsync_StockCap_AddsWarning_AndCapsQuantity));
        var product = await SeedProductAsync(context, "Product B", 5, true, true);
        await SeedCartItemAsync(context, userId, product.Id, 2);

        var service = BuildService(context);

        var result = await service.MergeGuestCartAsync(userId, new MergeCartRequestDto
        {
            Items =
            [
                new MergeCartItemDto { ProductId = product.Id, Quantity = 5 }
            ]
        });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        var merged = Assert.Single(result.Data!.Cart.Items);
        Assert.Equal(5, merged.Quantity);
        Assert.Contains(result.Data.Warnings, x => x.Code == "QUANTITY_CAPPED");
    }

    [Fact]
    public async Task MergeGuestCartAsync_InactiveProducts_AreSkippedWithWarnings()
    {
        var userId = Guid.NewGuid();
        await using var context = BuildContext(nameof(MergeGuestCartAsync_InactiveProducts_AreSkippedWithWarnings));
        var inactive = await SeedProductAsync(context, "Product C", 10, false, true);

        var service = BuildService(context);

        var result = await service.MergeGuestCartAsync(userId, new MergeCartRequestDto
        {
            Items =
            [
                new MergeCartItemDto { ProductId = inactive.Id, Quantity = 2 }
            ]
        });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data!.Cart.Items);
        Assert.Contains(result.Data.Warnings, x => x.Code == "PRODUCT_UNAVAILABLE");
    }

    [Fact]
    public async Task MergeGuestCartAsync_EmptyGuestList_IsNoOp()
    {
        var userId = Guid.NewGuid();
        await using var context = BuildContext(nameof(MergeGuestCartAsync_EmptyGuestList_IsNoOp));
        var product = await SeedProductAsync(context, "Product D", 20, true, true);
        await SeedCartItemAsync(context, userId, product.Id, 2);

        var service = BuildService(context);

        var result = await service.MergeGuestCartAsync(userId, new MergeCartRequestDto
        {
            Items = []
        });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data!.Cart.Items);
        Assert.Equal(2, result.Data.Cart.Items[0].Quantity);
        Assert.Empty(result.Data.Warnings);
    }

    private static CartService BuildService(ApplicationDbContext context)
    {
        var carts = new CartRepository(context);
        var products = new ProductRepository(context);
        var uow = new CartMergeTestUnitOfWork(context, carts, products);
        return new CartService(uow, NullLogger<CartService>.Instance);
    }

    private static ApplicationDbContext BuildContext(string testName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"tbm-cart-merge-{testName}-{Guid.NewGuid():N}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task<Product> SeedProductAsync(
        ApplicationDbContext context,
        string name,
        int stockQuantity,
        bool isActive,
        bool trackInventory)
    {
        var category = await context.Categories.FirstOrDefaultAsync();
        if (category == null)
        {
            category = new Category
            {
                Name = "General",
                Description = "General category",
                Slug = "general",
                BrandType = BrandType.TBM
            };

            context.Categories.Add(category);
            await context.SaveChangesAsync();
        }

        var product = new Product
        {
            Name = name,
            Description = $"{name} description",
            ShortDescription = $"{name} short",
            Slug = $"{name.ToLowerInvariant().Replace(" ", "-")}-{Guid.NewGuid():N}",
            CategoryId = category.Id,
            BrandType = BrandType.TBM,
            ProductType = ProductType.PhysicalProduct,
            Price = 1000m,
            IsActive = isActive,
            IsFeatured = false,
            TrackInventory = trackInventory,
            StockQuantity = stockQuantity
        };

        context.Products.Add(product);
        await context.SaveChangesAsync();
        return product;
    }

    private static async Task SeedCartItemAsync(
        ApplicationDbContext context,
        Guid userId,
        Guid productId,
        int quantity)
    {
        var cart = await context.Carts.FirstOrDefaultAsync(x => x.UserId == userId);
        if (cart == null)
        {
            cart = new Cart
            {
                UserId = userId,
                ExpiresAt = DateTime.UtcNow.AddDays(30)
            };
            context.Carts.Add(cart);
            await context.SaveChangesAsync();
        }

        context.CartItems.Add(new CartItem
        {
            CartId = cart.Id,
            ProductId = productId,
            Quantity = quantity,
            UnitPrice = 1000m,
            AddedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }

    private sealed class CartMergeTestUnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;

        public CartMergeTestUnitOfWork(ApplicationDbContext context, ICartRepository carts, IProductRepository products)
        {
            _context = context;
            Carts = carts;
            Products = products;
        }

        public IUserRepository Users => throw new NotImplementedException();
        public IUserAddressRepository UserAddresses => throw new NotImplementedException();
        public IRoleRepository Roles => throw new NotImplementedException();
        public IAIProjectRepository AIProjects => throw new NotImplementedException();
        public IAIDesignRepository AIDesigns => throw new NotImplementedException();
        public IAIUsageRepository AIUsages => throw new NotImplementedException();
        public IAIRenovationEstimateRepository AIRenovationEstimates => throw new NotImplementedException();
        public IAIAssistantRepository AIAssistant => throw new NotImplementedException();
        public ISettingRepository Settings => throw new NotImplementedException();
        public IAuditLogRepository AuditLogs => throw new NotImplementedException();
        public ICategoryRepository Categories => throw new NotImplementedException();
        public IProductRepository Products { get; }
        public IProductImageRepository ProductImages => throw new NotImplementedException();
        public ICartRepository Carts { get; }
        public IOrderRepository Orders => throw new NotImplementedException();
        public IOrderStatusHistoryRepository OrderStatusHistories => throw new NotImplementedException();
        public IWebhookEventRepository WebhookEvents => throw new NotImplementedException();
        public IDesignSessionRepository DesignSessions => throw new NotImplementedException();
        public IBillOfMaterialsRepository BillsOfMaterials => throw new NotImplementedException();
        public IProjectRepository Projects => throw new NotImplementedException();
        public ISubscriptionRepository Subscriptions => throw new NotImplementedException();
        public IPricingConfigRepository PricingConfigs => throw new NotImplementedException();
        public IDiscountRepository Discounts => throw new NotImplementedException();
        public IPortfolioRepository Portfolio => throw new NotImplementedException();

        public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();

        public void ClearChangeTracker() => _context.ChangeTracker.Clear();

        public Task BeginTransactionAsync() => Task.CompletedTask;

        public Task CommitTransactionAsync() => Task.CompletedTask;

        public Task RollbackTransactionAsync() => Task.CompletedTask;

        public Task ExecuteInTransactionAsync(Func<Task> operation) => operation();

        public Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation) => operation();

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
