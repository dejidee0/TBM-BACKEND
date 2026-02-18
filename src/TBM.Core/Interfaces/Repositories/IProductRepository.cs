using TBM.Core.Entities.Products;
using TBM.Core.Enums;

namespace TBM.Core.Interfaces.Repositories;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id);
    Task<Product?> GetBySlugAsync(string slug);
    Task<(IEnumerable<Product> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        BrandType? brandType = null,
        ProductType? productType = null,
        Guid? categoryId = null,
        string? searchTerm = null,
        bool? isFeatured = null,
        bool activeOnly = true
    );
    Task<IEnumerable<Product>> GetFeaturedAsync(BrandType? brandType = null, int limit = 10);
    Task<IEnumerable<Product>> GetRelatedAsync(Guid productId, int limit = 4);
    Task<Product> CreateAsync(Product product);
    Task UpdateAsync(Product product);
    Task DeleteAsync(Guid id);
    Task<bool> SlugExistsAsync(string slug, Guid? excludeId = null);
    Task<bool> SKUExistsAsync(string sku, Guid? excludeId = null);
    Task UpdateStockAsync(Guid productId, int quantity);
}