using TBM.Core.Entities.Products;

namespace TBM.Core.Interfaces.Repositories;

public interface IProductVariantRepository
{
    Task<ProductVariant> CreateAsync(ProductVariant variant);
    Task DeleteAsync(Guid id);
}
