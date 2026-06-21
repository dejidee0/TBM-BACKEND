using TBM.Application.DTOs.Orders;
using TBM.Application.Services;
using TBM.Application.Services.DesignFlow;
using TBM.Application.Tests.TestDoubles;

namespace TBM.Application.Tests.Services;

public class OrderServiceIdempotencyTests
{
    // Locks in the duplicate-order guard: when the same cart is checked out
    // twice with the same (deterministic) payment reference, CreateOrderAsync
    // must return the SAME order instead of creating a second one. This depends
    // on PaymentReference being persisted at creation so the dedup lookup finds
    // the first order on the repeat submit.
    [Fact]
    public async Task CreateOrderAsync_with_same_payment_reference_returns_same_order()
    {
        var products = new FakeProductRepository();
        var billsOfMaterials = new FakeBillOfMaterialsRepository();
        var designSessions = new FakeDesignSessionRepository();
        var projects = new FakeProjectRepository();
        var orders = new FakeOrderRepository();
        var unitOfWork = new FakeUnitOfWork(products, billsOfMaterials, designSessions, projects, orders);

        var projectService = new ProjectService(
            unitOfWork,
            new NoopImageStorageService(),
            new ProjectTimelineService(unitOfWork),
            new NoopLogger<ProjectService>());

        var service = new OrderService(unitOfWork, projectService, new NoopLogger<OrderService>());

        var userId = Guid.NewGuid();
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Walnut side table",
            SKU = "TBL-001",
            IsActive = true,
            TrackInventory = false
        };
        products.Seed(product);

        unitOfWork.CartStore.Seed(new Cart
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Items = new List<CartItem>
            {
                new()
                {
                    ProductId = product.Id,
                    Product = product,
                    Quantity = 2,
                    UnitPrice = 5000m,
                    AddedAt = DateTime.UtcNow
                }
            }
        });

        // Same reference both times == the deterministic key the checkout layer
        // produces for an unchanged cart.
        var dto = new CreateOrderDto
        {
            PaymentReference = "TBM-CART-DETERMINISTIC-KEY",
            ShippingFullName = "Test User",
            ShippingPhone = "08012345678",
            ShippingAddress = "123 Test Street",
            ShippingCity = "Lagos",
            ShippingState = "Lagos"
        };

        var first = await service.CreateOrderAsync(userId, dto);
        var second = await service.CreateOrderAsync(userId, dto);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.NotNull(first.Data);
        Assert.NotNull(second.Data);

        // Same order returned, and only one order ever created.
        Assert.Equal(first.Data!.Id, second.Data!.Id);
        Assert.Single(await orders.GetUserOrdersAsync(userId));
    }
}
