using TBM.Application.Tests.TestDoubles;

namespace TBM.Application.Tests.Services.DesignFlow;

public class BOMGenerationServiceTests
{
    [Fact]
    public async Task GenerateBOMFromInventoryAsync_filters_inventory_by_tier_and_rejects_unknown_skus()
    {
        var products = new FakeProductRepository();
        var billsOfMaterials = new FakeBillOfMaterialsRepository();
        var designSessions = new FakeDesignSessionRepository();
        var projects = new FakeProjectRepository();
        var orders = new FakeOrderRepository();
        var unitOfWork = new FakeUnitOfWork(products, billsOfMaterials, designSessions, projects, orders);
        var bomClient = new CapturingBomGenerationClient
        {
            Response = new BomGenerationResultDto
            {
                Items =
                {
                    new BomGenerationItemDto
                    {
                        SKU = "BUD-001",
                        Quantity = 3,
                        Reason = "Budget accent"
                    },
                    new BomGenerationItemDto
                    {
                        SKU = "FAKE-999",
                        Quantity = 7,
                        Reason = "Hallucinated item"
                    }
                }
            }
        };

        var budgetProduct = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Decor Vase",
            Description = "Simple table decor",
            SKU = "BUD-001",
            BrandType = BrandType.TBM,
            ProductType = ProductType.PhysicalProduct,
            CategoryId = Guid.NewGuid(),
            Price = 60m,
            StockQuantity = 10,
            TrackInventory = true,
            IsActive = true,
            AIKeywords = "decor,accent",
            MaterialType = "ceramic",
            QualityTier = "Budget",
            RecommendedFor = "living room"
        };

        var luxuryProduct = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Premium Marble Tile",
            Description = "High-end flooring tile",
            SKU = "LUX-001",
            BrandType = BrandType.TBM,
            ProductType = ProductType.PhysicalProduct,
            CategoryId = Guid.NewGuid(),
            Price = 250m,
            StockQuantity = 8,
            TrackInventory = true,
            IsActive = true,
            AIKeywords = "tile,floor",
            MaterialType = "stone",
            QualityTier = "Luxury",
            RecommendedFor = "floor"
        };

        products.Seed(budgetProduct);
        products.Seed(luxuryProduct);

        var service = new BOMGenerationService(
            unitOfWork,
            bomClient,
            new NoopLogger<BOMGenerationService>());

        var session = new DesignSession
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            SessionNumber = "DS-2026-00001",
            ProjectName = "Guest Lounge Refresh",
            RoomType = "Living Room",
            VisionText = "Warm, modern, and minimal",
            Tier = DesignSessionTier.Economic,
            RoomLength = 2m,
            RoomWidth = 2m,
            RoomHeight = 2.5m
        };

        var bom = await service.GenerateBOMFromInventoryAsync(session, CancellationToken.None);

        Assert.NotNull(bom);
        Assert.Single(bom!.Items);
        Assert.Single(billsOfMaterials.CreatedBoms);
        Assert.Single(bomClient.Requests);
        Assert.Single(bomClient.Requests[0].Inventory);
        Assert.Equal("BUD-001", bomClient.Requests[0].Inventory[0].SKU);

        var item = bom.Items.Single();
        Assert.Equal(budgetProduct.Id, item.ProductId);
        Assert.Equal("BUD-001", item.SKU);
        Assert.Equal(3m, item.Quantity);
        Assert.Equal(60m, item.UnitPrice);
        Assert.Equal(180m, item.TotalPrice);
        Assert.True(item.InStock);
    }
}
