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
            .FirstOrDefaultAsync(p => p.Id == id);
    }
    
    public async Task<Product?> GetBySlugAsync(string slug)
    {
        return await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
            .FirstOrDefaultAsync(p => p.Slug == slug);
    }
    
    public async Task<(IEnumerable<Product> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        BrandType? brandType = null,
        ProductType? productType = null,
        Guid? categoryId = null,
        string? searchTerm = null,
        bool? isFeatured = null,
        bool activeOnly = true)
    {
        var query = _context.Products
            .Include(p => p.Category)
            .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
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
    
    public async Task UpdateStockAsync(Guid productId, int quantity)
    {
        var product = await _context.Products.FindAsync(productId);
        if (product != null && product.TrackInventory)
        {
            product.StockQuantity = quantity;
            _context.Products.Update(product);
        }
    }
}