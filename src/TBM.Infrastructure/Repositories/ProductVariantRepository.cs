using TBM.Core.Entities.Products;
using TBM.Core.Interfaces.Repositories;
using TBM.Infrastructure.Data;

namespace TBM.Infrastructure.Repositories;

public class ProductVariantRepository : IProductVariantRepository
{
    private readonly ApplicationDbContext _context;

    public ProductVariantRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ProductVariant> CreateAsync(ProductVariant variant)
    {
        await _context.ProductVariants.AddAsync(variant);
        return variant;
    }

    public async Task DeleteAsync(Guid id)
    {
        var variant = await _context.ProductVariants.FindAsync(id);
        if (variant != null)
        {
            _context.ProductVariants.Remove(variant);
        }
    }
}
