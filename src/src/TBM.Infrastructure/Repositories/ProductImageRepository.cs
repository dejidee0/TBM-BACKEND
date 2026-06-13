using Microsoft.EntityFrameworkCore;
using TBM.Core.Entities.Products;
using TBM.Core.Interfaces.Repositories;
using TBM.Infrastructure.Data;

namespace TBM.Infrastructure.Repositories;

public class ProductImageRepository : IProductImageRepository
{
    private readonly ApplicationDbContext _context;
    
    public ProductImageRepository(ApplicationDbContext context)
    {
        _context = context;
    }
    
    public async Task<ProductImage?> GetByIdAsync(Guid id)
    {
        return await _context.ProductImages.FindAsync(id);
    }
    
    public async Task<IEnumerable<ProductImage>> GetByProductIdAsync(Guid productId)
    {
        return await _context.ProductImages
            .Where(i => i.ProductId == productId)
            .OrderBy(i => i.DisplayOrder)
            .ToListAsync();
    }
    
    public async Task<ProductImage> CreateAsync(ProductImage image)
    {
        await _context.ProductImages.AddAsync(image);
        return image;
    }
    
    public Task UpdateAsync(ProductImage image)
    {
        _context.ProductImages.Update(image);
        return Task.CompletedTask;
    }
    
    public async Task DeleteAsync(Guid id)
    {
        var image = await _context.ProductImages.FindAsync(id);
        if (image != null)
        {
            _context.ProductImages.Remove(image);
        }
    }
    
    public async Task SetPrimaryImageAsync(Guid productId, Guid imageId)
    {
        // Remove primary flag from all images
        var images = await _context.ProductImages
            .Where(i => i.ProductId == productId)
            .ToListAsync();
        
        foreach (var image in images)
        {
            image.IsPrimary = image.Id == imageId;
        }
        
        _context.ProductImages.UpdateRange(images);
    }
}