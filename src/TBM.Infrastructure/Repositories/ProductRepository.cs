using Microsoft.EntityFrameworkCore;
using TBM.Core.Entities.Products;
using TBM.Core.Enums;
using TBM.Core.Interfaces.Repositories;
using TBM.Infrastructure.Data;

namespace TBM.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly ApplicationDbContext _context;
    
    public ProductRepository(ApplicationDbContext context)
    {
        _context = context;
    }
    
    public async Task<Product?> GetByIdAsync(Guid id)
    {
        return await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
            .Include(p => p.Variants.OrderBy(v => v.DisplayOrder))
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Product?> GetBySlugAsync(string slug)
    {
        return await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
            .Include(p => p.Variants.OrderBy(v => v.DisplayOrder))
            .FirstOrDefaultAsync(p => p.Slug == slug);
    }
    
    public async Task<(IEnumerable<Product> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        BrandType? brandType = null,
        ProductType? productType = null,
        Guid? categoryId = null,
        string? searchTerm = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        bool? isFeatured = null,
        bool activeOnly = true)
    {
        var query = _context.Products
            .Include(p => p.Category)
            .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
            .Include(p => p.Variants.OrderBy(v => v.DisplayOrder))
            .AsQueryable();
        
        if (activeOnly)
        {
            query = query.Where(p => p.IsActive);
        }
        
        if (brandType.HasValue)
        {
            query = query.Where(p => p.BrandType == brandType.Value);
        }
        
        if (productType.HasValue)
        {
            query = query.Where(p => p.ProductType == productType.Value);
        }
        
        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }
        
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(p => 
                p.Name.Contains(searchTerm) || 
                p.Description.Contains(searchTerm) ||
                p.ShortDescription.Contains(searchTerm) ||
                (p.SKU != null && p.SKU.Contains(searchTerm)) ||
                (p.Tags != null && p.Tags.Contains(searchTerm))
            );
        }

        if (minPrice.HasValue)
        {
            query = query.Where(p => p.Price.HasValue && p.Price.Value >= minPrice.Value);
        }

        if (maxPrice.HasValue)
        {
            query = query.Where(p => p.Price.HasValue && p.Price.Value <= maxPrice.Value);
        }
        
        if (isFeatured.HasValue)
        {
            query = query.Where(p => p.IsFeatured == isFeatured.Value);
        }
        
        var totalCount = await query.CountAsync();
        
        var items = await query
            .OrderBy(p => p.DisplayOrder)
            .ThenByDescending(p => p.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        
        return (items, totalCount);
    }
    
    public async Task<IEnumerable<Product>> GetFeaturedAsync(BrandType? brandType = null, int limit = 10)
    {
        var query = _context.Products
            .Include(p => p.Category)
            .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
            .Include(p => p.Variants.OrderBy(v => v.DisplayOrder))
            .Where(p => p.IsActive && p.IsFeatured);
        
        if (brandType.HasValue)
        {
            query = query.Where(p => p.BrandType == brandType.Value);
        }
        
        return await query
            .OrderBy(p => p.DisplayOrder)
            .Take(limit)
            .ToListAsync();
    }
    
    public async Task<IEnumerable<Product>> GetRelatedAsync(Guid productId, int limit = 4)
    {
        var product = await _context.Products.FindAsync(productId);
        if (product == null) return Enumerable.Empty<Product>();
        
        return await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
            .Include(p => p.Variants.OrderBy(v => v.DisplayOrder))
            .Where(p =>
                p.Id != productId &&
                p.IsActive &&
                (p.CategoryId == product.CategoryId || p.BrandType == product.BrandType)
            )
            .OrderBy(p => Guid.NewGuid()) // Random order
            .Take(limit)
            .ToListAsync();
    }
    
    public async Task<Product> CreateAsync(Product product)
    {
        await _context.Products.AddAsync(product);
        return product;
    }
    
    public Task UpdateAsync(Product product)
    {
        _context.Products.Update(product);
        return Task.CompletedTask;
    }
    
    public async Task DeleteAsync(Guid id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product != null)
        {
            product.IsDeleted = true;
            product.DeletedAt = DateTime.UtcNow;
            _context.Products.Update(product);
        }
    }
    
    public async Task<bool> SlugExistsAsync(string slug, Guid? excludeId = null)
    {
        var query = _context.Products.Where(p => p.Slug == slug);
        
        if (excludeId.HasValue)
        {
            query = query.Where(p => p.Id != excludeId.Value);
        }
        
        return await query.AnyAsync();
    }
    
    public async Task<bool> SKUExistsAsync(string sku, Guid? excludeId = null)
    {
        var query = _context.Products.Where(p => p.SKU == sku);
        
        if (excludeId.HasValue)
        {
            query = query.Where(p => p.Id != excludeId.Value);
        }
        
        return await query.AnyAsync();
    }

    public async Task<List<Product>> GetInventoryCandidatesAsync()
    {
        return await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
            .Where(p =>
                p.IsActive &&
                p.TrackInventory &&
                p.StockQuantity.HasValue &&
                p.StockQuantity.Value > 0 &&
                p.Price.HasValue &&
                p.ProductType == ProductType.PhysicalProduct)
            .OrderBy(p => p.DisplayOrder)
            .ThenByDescending(p => p.CreatedAt)
            .ToListAsync();
    }
    
    public async Task UpdateStockAsync(Guid productId, int quantity)
    {
        var product = await _context.Products.FindAsync(productId);
        if (product != null && product.TrackInventory)
        {
            product.StockQuantity = quantity;
            _context.Products.Update(product);
        }
    }
    
    public async Task<int> UpdateStockAtomicAsync(Guid productId, int quantityToSubtract)
    {
        // Atomic update: decrements stock directly in SQL to avoid concurrency issues
        // Returns the number of rows affected (0 if product not found, doesn't track inventory, or has insufficient stock)
        // This also prevents negative stock by checking that current stock >= quantity to subtract
        return await _context.Database.ExecuteSqlRawAsync(
            "UPDATE Products SET StockQuantity = StockQuantity - {0} WHERE Id = {1} AND TrackInventory = 1 AND StockQuantity IS NOT NULL AND StockQuantity >= {0}",
            quantityToSubtract, productId);
    }

    /// <summary>
    /// Finds active Bogat products whose AI metadata (AIKeywords, MaterialType,
    /// RecommendedFor, Tags, Name) contains any of the supplied keywords.
    /// Used to surface purchasable materials after an AI design generation.
    /// </summary>
    public async Task<List<Product>> SearchByAIKeywordsAsync(IEnumerable<string> keywords, int limit = 8)
    {
        var kws = keywords
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim().ToLower())
            .Distinct()
            .ToList();

        if (kws.Count == 0)
            return new List<Product>();

        var query = _context.Products
            .Include(p => p.Category)
            .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
            .Where(p => p.IsActive && !p.IsDeleted);

        // Build OR predicate: product matches if ANY keyword appears in ANY of its metadata fields.
        // EF Core translates each Contains() to SQL LIKE '%keyword%'.
        query = query.Where(p => kws.Any(kw =>
            (p.AIKeywords != null && p.AIKeywords.ToLower().Contains(kw)) ||
            (p.MaterialType != null && p.MaterialType.ToLower().Contains(kw)) ||
            (p.RecommendedFor != null && p.RecommendedFor.ToLower().Contains(kw)) ||
            (p.Tags != null && p.Tags.ToLower().Contains(kw)) ||
            p.Name.ToLower().Contains(kw) ||
            (p.ShortDescription != null && p.ShortDescription.ToLower().Contains(kw))
        ));

        return await query
            .OrderByDescending(p => p.IsFeatured)
            .ThenBy(p => p.DisplayOrder)
            .Take(limit)
            .ToListAsync();
    }

    /// <summary>
    /// Bulk-inserts a batch of products in a single round-trip.
    /// Caller is responsible for calling SaveChangesAsync.
    /// </summary>
    public async Task<List<Product>> BulkCreateAsync(IEnumerable<Product> products)
    {
        var list = products.ToList();
        await _context.Products.AddRangeAsync(list);
        return list;
    }

    public async Task<Dictionary<Guid, int>> GetActiveProductCountsAsync(IEnumerable<Guid> categoryIds)
    {
        var ids = categoryIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        return await _context.Products
            .Where(p => p.IsActive && ids.Contains(p.CategoryId))
            .GroupBy(p => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count);
    }
}
