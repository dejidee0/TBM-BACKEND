using Microsoft.EntityFrameworkCore;
using TBM.Core.Entities.Products;
using TBM.Core.Enums;
using TBM.Infrastructure.Data;
using TBM.Infrastructure.Repositories;

namespace TBM.API.ContractTests;

public sealed class ProductRepositoryFilterTests
{
    [Fact]
    public async Task GetPagedAsync_WithMinPriceOnly_FiltersServerSide()
    {
        await using var context = BuildContext(nameof(GetPagedAsync_WithMinPriceOnly_FiltersServerSide));
        await SeedProductsAsync(context);
        var repository = new ProductRepository(context);

        var (items, total) = await repository.GetPagedAsync(
            pageNumber: 1,
            pageSize: 20,
            minPrice: 20000m);

        Assert.Equal(1, total);
        Assert.All(items, item => Assert.True(item.Price >= 20000m));
    }

    [Fact]
    public async Task GetPagedAsync_WithMaxPriceOnly_FiltersServerSide()
    {
        await using var context = BuildContext(nameof(GetPagedAsync_WithMaxPriceOnly_FiltersServerSide));
        await SeedProductsAsync(context);
        var repository = new ProductRepository(context);

        var (items, total) = await repository.GetPagedAsync(
            pageNumber: 1,
            pageSize: 20,
            maxPrice: 18000m);

        Assert.Equal(2, total);
        Assert.All(items, item => Assert.True(item.Price <= 18000m));
    }

    [Fact]
    public async Task GetPagedAsync_WithMinAndMaxPrice_FiltersRange()
    {
        await using var context = BuildContext(nameof(GetPagedAsync_WithMinAndMaxPrice_FiltersRange));
        await SeedProductsAsync(context);
        var repository = new ProductRepository(context);

        var (items, total) = await repository.GetPagedAsync(
            pageNumber: 1,
            pageSize: 20,
            minPrice: 14000m,
            maxPrice: 22000m);

        Assert.Equal(1, total);
        Assert.All(items, item =>
        {
            Assert.True(item.Price >= 14000m);
            Assert.True(item.Price <= 22000m);
        });
    }

    [Fact]
    public async Task GetPagedAsync_WithFeaturedAndPriceRange_CombinesFilters()
    {
        await using var context = BuildContext(nameof(GetPagedAsync_WithFeaturedAndPriceRange_CombinesFilters));
        await SeedProductsAsync(context);
        var repository = new ProductRepository(context);

        var (items, total) = await repository.GetPagedAsync(
            pageNumber: 1,
            pageSize: 20,
            minPrice: 14000m,
            maxPrice: 26000m,
            isFeatured: true);

        Assert.Equal(1, total);
        var item = Assert.Single(items);
        Assert.Equal("Marble Premium", item.Name);
    }

    private static ApplicationDbContext BuildContext(string testName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"tbm-product-filters-{testName}-{Guid.NewGuid():N}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task SeedProductsAsync(ApplicationDbContext context)
    {
        var category = new Category
        {
            Name = "Tiles",
            Description = "Tile materials",
            Slug = "tiles",
            BrandType = BrandType.TBM
        };

        context.Categories.Add(category);

        context.Products.AddRange(
            new Product
            {
                Name = "Ceramic Basic",
                Description = "Ceramic tile",
                ShortDescription = "Ceramic",
                Slug = "ceramic-basic",
                Category = category,
                BrandType = BrandType.TBM,
                ProductType = ProductType.PhysicalProduct,
                Price = 12000m,
                ShowPrice = true,
                IsActive = true,
                IsFeatured = false
            },
            new Product
            {
                Name = "Porcelain Prime",
                Description = "Porcelain tile",
                ShortDescription = "Porcelain",
                Slug = "porcelain-prime",
                Category = category,
                BrandType = BrandType.TBM,
                ProductType = ProductType.PhysicalProduct,
                Price = 18000m,
                ShowPrice = true,
                IsActive = true,
                IsFeatured = false
            },
            new Product
            {
                Name = "Marble Premium",
                Description = "Marble tile",
                ShortDescription = "Marble",
                Slug = "marble-premium",
                Category = category,
                BrandType = BrandType.TBM,
                ProductType = ProductType.PhysicalProduct,
                Price = 25000m,
                ShowPrice = true,
                IsActive = true,
                IsFeatured = true
            },
            new Product
            {
                Name = "Hidden Inactive",
                Description = "Inactive tile",
                ShortDescription = "Inactive",
                Slug = "hidden-inactive",
                Category = category,
                BrandType = BrandType.TBM,
                ProductType = ProductType.PhysicalProduct,
                Price = 50000m,
                ShowPrice = true,
                IsActive = false,
                IsFeatured = true
            });

        await context.SaveChangesAsync();
    }
}
