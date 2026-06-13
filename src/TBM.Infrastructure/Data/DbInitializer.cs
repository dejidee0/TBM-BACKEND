using Microsoft.EntityFrameworkCore;
using TBM.Core.Entities.Products;
using TBM.Core.Entities.Users;
using TBM.Core.Enums;

namespace TBM.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        // Keep dev/local aligned with EF migrations and avoid schema drift from EnsureCreated.
        await context.Database.MigrateAsync();

        // Seed Roles if they don't exist
        if (!context.Roles.Any())
        {
            var roles = new List<Role>
            {
                new()
                {
                    Name = UserRoles.Customer,
                    Description = "Regular customer with access to shop and purchase"
                },
                new()
                {
                    Name = UserRoles.Vendor,
                    Description = "Vendor with isolated catalog and fulfillment scope"
                },
                new()
                {
                    Name = UserRoles.Admin,
                    Description = "Administrator with operational access"
                },
                new()
                {
                    Name = UserRoles.SuperAdmin,
                    Description = "Super administrator with full system access"
                }
            };
            
            await context.Roles.AddRangeAsync(roles);
            await context.SaveChangesAsync();
        }
        else
        {
            var requiredRoles = new[]
            {
                new { Name = UserRoles.Customer, Description = "Regular customer with access to shop and purchase" },
                new { Name = UserRoles.Vendor, Description = "Vendor with isolated catalog and fulfillment scope" },
                new { Name = UserRoles.Admin, Description = "Administrator with operational access" },
                new { Name = UserRoles.SuperAdmin, Description = "Super administrator with full system access" }
            };

            var existingRoleNames = context.Roles.Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missingRoles = requiredRoles
                .Where(r => !existingRoleNames.Contains(r.Name))
                .Select(r => new Role { Name = r.Name, Description = r.Description })
                .ToList();

            if (missingRoles.Any())
            {
                await context.Roles.AddRangeAsync(missingRoles);
                await context.SaveChangesAsync();
            }
        }
        
        // Seed Categories if they don't exist
        if (!context.Categories.Any())
        {
            var categories = new List<Category>
            {
                // Bogat Categories
                new()
                {
                    Name = "Bathroom Fixtures",
                    Description = "Complete bathroom fixtures and fittings",
                    Slug = "bathroom-fixtures",
                    BrandType = BrandType.Bogat,
                    DisplayOrder = 1,
                    IsActive = true
                },
                new()
                {
                    Name = "Kitchen Fixtures",
                    Description = "Modern kitchen fixtures and accessories",
                    Slug = "kitchen-fixtures",
                    BrandType = BrandType.Bogat,
                    DisplayOrder = 2,
                    IsActive = true
                },
                new()
                {
                    Name = "Tiles & Flooring",
                    Description = "Premium tiles and flooring solutions",
                    Slug = "tiles-flooring",
                    BrandType = BrandType.Bogat,
                    DisplayOrder = 3,
                    IsActive = true
                },
                new()
                {
                    Name = "Building Materials",
                    Description = "Quality building materials",
                    Slug = "building-materials",
                    BrandType = BrandType.Bogat,
                    DisplayOrder = 4,
                    IsActive = true
                },
                
                // TBM Service Categories
                new()
                {
                    Name = "Construction",
                    Description = "Professional construction services",
                    Slug = "construction",
                    BrandType = BrandType.TBM,
                    DisplayOrder = 1,
                    IsActive = true
                },
                new()
                {
                    Name = "Renovation",
                    Description = "Complete renovation and remodeling services",
                    Slug = "renovation",
                    BrandType = BrandType.TBM,
                    DisplayOrder = 2,
                    IsActive = true
                },
                new()
                {
                    Name = "Maintenance",
                    Description = "Regular maintenance and repair services",
                    Slug = "maintenance",
                    BrandType = BrandType.TBM,
                    DisplayOrder = 3,
                    IsActive = true
                }
            };
            
            await context.Categories.AddRangeAsync(categories);
            await context.SaveChangesAsync();
        }
        
        // Seed Sample Products if they don't exist
        if (!context.Products.Any())
        {
            var bathroomCategory = context.Categories.First(c => c.Slug == "bathroom-fixtures");
            var constructionCategory = context.Categories.First(c => c.Slug == "construction");
            
            var products = new List<Product>
            {
                // Sample Bogat Product
                new()
                {
                    Name = "Premium Wall-Mounted WC",
                    Description = "High-quality wall-mounted water closet with soft-close seat. Modern design with efficient flushing system.",
                    ShortDescription = "Premium wall-mounted WC with soft-close seat",
                    Slug = "premium-wall-mounted-wc",
                    SKU = "WC-001",
                    BrandType = BrandType.Bogat,
                    ProductType = ProductType.PhysicalProduct,
                    CategoryId = bathroomCategory.Id,
                    Price = 85000,
                    CompareAtPrice = 95000,
                    ShowPrice = true,
                    StockQuantity = 25,
                    LowStockThreshold = 5,
                    TrackInventory = true,
                    IsActive = true,
                    IsFeatured = true,
                    DisplayOrder = 1,
                    Tags = "bathroom, wc, wall-mounted, premium",
                    AIKeywords = "toilet, wc, sanitary, bathroom",
                    MaterialType = "Ceramic",
                    QualityTier = "Luxury",
                    RecommendedFor = "Luxury bathrooms, master suites"
                },

                new()
                {
                    Name = "Standard Floor Tile",
                    Description = "Durable ceramic floor tile suitable for everyday use.",
                    ShortDescription = "Standard ceramic flooring tile",
                    Slug = "standard-floor-tile",
                    SKU = "TILE-STD-001",
                    BrandType = BrandType.Bogat,
                    ProductType = ProductType.PhysicalProduct,
                    CategoryId = context.Categories.First(c => c.Slug == "tiles-flooring").Id,
                    Price = 15000,
                    CompareAtPrice = 17000,
                    ShowPrice = true,
                    StockQuantity = 120,
                    LowStockThreshold = 20,
                    TrackInventory = true,
                    IsActive = true,
                    IsFeatured = false,
                    DisplayOrder = 2,
                    Tags = "flooring, tile, standard",
                    AIKeywords = "tile, flooring, ceramic, standard",
                    MaterialType = "Ceramic",
                    QualityTier = "Standard",
                    RecommendedFor = "General residential flooring"
                },

                new()
                {
                    Name = "Budget Wall Paint",
                    Description = "Affordable washable wall paint for interior spaces.",
                    ShortDescription = "Interior wall paint",
                    Slug = "budget-wall-paint",
                    SKU = "PAINT-BUD-001",
                    BrandType = BrandType.Bogat,
                    ProductType = ProductType.PhysicalProduct,
                    CategoryId = context.Categories.First(c => c.Slug == "building-materials").Id,
                    Price = 8500,
                    CompareAtPrice = 9500,
                    ShowPrice = true,
                    StockQuantity = 240,
                    LowStockThreshold = 30,
                    TrackInventory = true,
                    IsActive = true,
                    IsFeatured = false,
                    DisplayOrder = 3,
                    Tags = "paint, budget, interior",
                    AIKeywords = "paint, interior, wall, budget",
                    MaterialType = "Paint",
                    QualityTier = "Budget",
                    RecommendedFor = "Affordable interior refresh"
                },

                new()
                {
                    Name = "Premium Stone Tile",
                    Description = "Luxury stone-effect tile for high-end finishes.",
                    ShortDescription = "Luxury stone tile",
                    Slug = "premium-stone-tile",
                    SKU = "TILE-LUX-001",
                    BrandType = BrandType.Bogat,
                    ProductType = ProductType.PhysicalProduct,
                    CategoryId = context.Categories.First(c => c.Slug == "tiles-flooring").Id,
                    Price = 32500,
                    CompareAtPrice = 36000,
                    ShowPrice = true,
                    StockQuantity = 75,
                    LowStockThreshold = 10,
                    TrackInventory = true,
                    IsActive = true,
                    IsFeatured = true,
                    DisplayOrder = 4,
                    Tags = "tile, stone, luxury",
                    AIKeywords = "tile, stone, luxury, premium",
                    MaterialType = "Stone",
                    QualityTier = "Premium",
                    RecommendedFor = "Luxury interiors and feature walls"
                },
                
                // Sample TBM Service
                new()
                {
                    Name = "Residential Construction",
                    Description = "Complete residential construction services including foundation, roofing, plumbing, and electrical work. We deliver quality homes on time and within budget.",
                    ShortDescription = "Complete residential construction services",
                    Slug = "residential-construction",
                    BrandType = BrandType.TBM,
                    ProductType = ProductType.Service,
                    CategoryId = constructionCategory.Id,
                    ShowPrice = false, // Request quote
                    TrackInventory = false,
                    IsActive = true,
                    IsFeatured = true,
                    DisplayOrder = 1,
                    Tags = "construction, residential, building",
                    AIKeywords = "construction, service, residential",
                    MaterialType = "Service",
                    QualityTier = "Standard",
                    RecommendedFor = "Construction service"
                }
            };
            
            await context.Products.AddRangeAsync(products);
            await context.SaveChangesAsync();
        }
    }
}
