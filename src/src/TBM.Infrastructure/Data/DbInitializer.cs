using TBM.Core.Entities.Products;
using TBM.Core.Entities.Users;
using TBM.Core.Enums;

namespace TBM.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
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
                    Tags = "bathroom, wc, wall-mounted, premium"
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
                    Tags = "construction, residential, building"
                }
            };
            
            await context.Products.AddRangeAsync(products);
            await context.SaveChangesAsync();
        }
    }
}
