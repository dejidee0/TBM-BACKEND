using TBM.Core.Entities.Products;
using TBM.Core.Enums;

namespace TBM.Core.Interfaces.Repositories;

public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(Guid id);
    Task<Category?> GetBySlugAsync(string slug);
    Task<IEnumerable<Category>> GetAllAsync();
    Task<IEnumerable<Category>> GetByBrandAsync(BrandType brandType);
    Task<IEnumerable<Category>> GetRootCategoriesAsync(BrandType? brandType = null);
    Task<IEnumerable<Category>> GetSubCategoriesAsync(Guid parentId);
    Task<Category> CreateAsync(Category category);
    Task UpdateAsync(Category category);
    Task DeleteAsync(Guid id);
    Task<bool> SlugExistsAsync(string slug, Guid? excludeId = null);
}