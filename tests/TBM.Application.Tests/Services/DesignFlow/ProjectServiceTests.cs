using TBM.Application.Tests.TestDoubles;

namespace TBM.Application.Tests.Services.DesignFlow;

public class ProjectServiceTests
{
    [Fact]
    public async Task CreateProjectFromOrderAsync_creates_project_timeline_and_is_idempotent()
    {
        var products = new FakeProductRepository();
        var billsOfMaterials = new FakeBillOfMaterialsRepository();
        var designSessions = new FakeDesignSessionRepository();
        var projects = new FakeProjectRepository();
        var orders = new FakeOrderRepository();
        var unitOfWork = new FakeUnitOfWork(products, billsOfMaterials, designSessions, projects, orders);
        var timelineService = new ProjectTimelineService(unitOfWork);
        var service = new ProjectService(
            unitOfWork,
            new NoopImageStorageService(),
            timelineService,
            new NoopLogger<ProjectService>());

        var userId = Guid.NewGuid();
        var session = new DesignSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SessionNumber = "DS-2026-00002",
            ProjectName = "Master Bedroom Refresh",
            RoomType = "Bedroom",
            VisionText = "Warm luxury bedroom with walnut accents",
            Tier = DesignSessionTier.Luxury,
            BOMId = Guid.NewGuid(),
            Status = DesignSessionStatus.Ordered
        };

        var bom = new BillOfMaterials
        {
            Id = session.BOMId.Value,
            DesignSessionId = session.Id,
            BomNumber = "BOM-2026-00001",
            ItemCount = 2,
            TotalEstimatedCost = 1250m,
            Status = BillOfMaterialsStatus.Finalized
        };

        bom.Items.Add(new BOMItem
        {
            ProductId = Guid.NewGuid(),
            SKU = "BOM-001",
            Name = "Wardrobe handle",
            Description = "Premium hardware",
            Quantity = 2,
            UnitPrice = 100m,
            TotalPrice = 200m,
            InStock = true,
            Reason = "Selected from inventory"
        });

        bom.Items.Add(new BOMItem
        {
            ProductId = Guid.NewGuid(),
            SKU = "BOM-002",
            Name = "Bedside lamp",
            Description = "Warm ambient lighting",
            Quantity = 1,
            UnitPrice = 150m,
            TotalPrice = 150m,
            InStock = true,
            Reason = "Selected from inventory"
        });

        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DesignSessionId = session.Id,
            OrderNumber = "ORD-2026-00001",
            PaymentStatus = PaymentStatus.Paid,
            Total = 1000m,
            SubTotal = 1000m,
            ShippingCost = 0m,
            Tax = 0m,
            Discount = 0m,
            Status = OrderStatus.Processing
        };

        designSessions.Seed(session);
        billsOfMaterials.Seed(bom);
        orders.Seed(order);

        var created = await service.CreateProjectFromOrderAsync(order.Id, CancellationToken.None);
        var repeated = await service.CreateProjectFromOrderAsync(order.Id, CancellationToken.None);

        Assert.NotNull(created);
        Assert.NotNull(repeated);
        Assert.Equal(created!.Id, repeated!.Id);
        Assert.Single(projects.CreatedProjects);
        Assert.Equal(ProjectStatus.InProgress, created.Status);
        Assert.Equal(order.Id, created.OrderId);
        Assert.Equal(session.Id, created.DesignSessionId);
        Assert.Equal(bom.Id, created.BOMId);
        Assert.Equal(6, created.Timelines.Count);
        Assert.Equal("Project kickoff", created.Timelines.First().MilestoneName);
        Assert.Equal("Quality check", created.Timelines.Last().MilestoneName);
        Assert.Equal(DesignSessionStatus.ConvertedToProject, session.Status);
        Assert.Equal(created.Id, session.ProjectId);
        Assert.Equal(1000m, created.AmountPaid);
        Assert.Equal(1250m, created.TotalBudget);
        Assert.Equal(250m, created.AmountPending);
    }
}
