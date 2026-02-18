using TBM.Core.Entities.Products;

namespace TBM.Core.Interfaces.Repositories;

public interface IProductImageRepository
{
    Task<ProductImage?> GetByIdAsync(Guid id);
    Task<IEnumerable<ProductImage>> GetByProductIdAsync(Guid productId);
    Task<ProductImage> CreateAsync(ProductImage image);
    Task UpdateAsync(ProductImage image);
    Task DeleteAsync(Guid id);
    Task SetPrimaryImageAsync(Guid productId, Guid imageId);
}