using Microsoft.EntityFrameworkCore;
using TBM.Core.Entities.Products;
using TBM.Core.Enums;
using TBM.Core.Interfaces.Repositories;
using TBM.Infrastructure.Data;

namespace TBM.Infrastructure.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly ApplicationDbContext _context;
    
    public CategoryRepository(ApplicationDbContext context)
    {
        _context = context;
    }
    
    public async Task<Category?> GetByIdAsync(Guid id)
    {
        return await _context.Categories
            .Include(c => c.SubCategories)
            .Include(c => c.ParentCategory)
            .FirstOrDefaultAsync(c => c.Id == id);
    }
    
    public async Task<Category?> GetBySlugAsync(string slug)
    {
        return await _context.Categories
            .Include(c => c.SubCategories)
            .Include(c => c.ParentCategory)
            .FirstOrDefaultAsync(c => c.Slug == slug);
    }
    
    public async Task<IEnumerable<Category>> GetAllAsync()
    {
        return await _context.Categories
            .Include(c => c.SubCategories)
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync();
    }
    
    public async Task<IEnumerable<Category>> GetByBrandAsync(BrandType brandType)
    {
        return await _context.Categories
            .Include(c => c.SubCategories)
            .Where(c => c.BrandType == brandType && c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync();
    }
    
    public async Task<IEnumerable<Category>> GetRootCategoriesAsync(BrandType? brandType = null)
    {
        var query = _context.Categories
            .Include(c => c.SubCategories)
            .Where(c => c.ParentCategoryId == null && c.IsActive);
        
        if (brandType.HasValue)
        {
            query = query.Where(c => c.BrandType == brandType.Value);
        }
        
        return await query
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync();
    }
    
    public async Task<IEnumerable<Category>> GetSubCategoriesAsync(Guid parentId)
    {
        return await _context.Categories
            .Where(c => c.ParentCategoryId == parentId && c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync();
    }
    
    public async Task<Category> CreateAsync(Category category)
    {
        await _context.Categories.AddAsync(category);
        return category;
    }
    
    public Task UpdateAsync(Category category)
    {
        _context.Categories.Update(category);
        return Task.CompletedTask;
    }
    
    public async Task DeleteAsync(Guid id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category != null)
        {
            category.IsDeleted = true;
            category.DeletedAt = DateTime.UtcNow;
            _context.Categories.Update(category);
        }
    }
    
    public async Task<bool> SlugExistsAsync(string slug, Guid? excludeId = null)
    {
        var query = _context.Categories.Where(c => c.Slug == slug);
        
        if (excludeId.HasValue)
        {
            query = query.Where(c => c.Id != excludeId.Value);
        }
        
        return await query.AnyAsync();
    }
}