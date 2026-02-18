using TBM.Application.DTOs.Common;
using TBM.Application.DTOs.Products;

namespace TBM.Application.Interfaces;

public interface IProductService
{
    // Category operations
    Task<ApiResponse<CategoryDto>> GetCategoryByIdAsync(Guid id);
    Task<ApiResponse<CategoryDto>> GetCategoryBySlugAsync(string slug);
    Task<ApiResponse<List<CategoryDto>>> GetAllCategoriesAsync();
    Task<ApiResponse<List<CategoryDto>>> GetCategoriesByBrandAsync(int brandType);
    Task<ApiResponse<CategoryDto>> CreateCategoryAsync(CreateCategoryDto dto);
    Task<ApiResponse<CategoryDto>> UpdateCategoryAsync(Guid id, UpdateCategoryDto dto);
    Task<ApiResponse<bool>> DeleteCategoryAsync(Guid id);
    
    // Product operations
    Task<ApiResponse<ProductDto>> GetProductByIdAsync(Guid id);
    Task<ApiResponse<ProductDto>> GetProductBySlugAsync(string slug);
    Task<ApiResponse<PagedResultDto<ProductDto>>> GetProductsAsync(ProductFilterDto filter);
    Task<ApiResponse<List<ProductDto>>> GetFeaturedProductsAsync(int? brandType = null, int limit = 10);
    Task<ApiResponse<List<ProductDto>>> GetRelatedProductsAsync(Guid productId, int limit = 4);
    Task<ApiResponse<ProductDto>> CreateProductAsync(CreateProductDto dto);
    Task<ApiResponse<ProductDto>> UpdateProductAsync(Guid id, UpdateProductDto dto);
    Task<ApiResponse<bool>> DeleteProductAsync(Guid id);
    
    // Product image operations
    Task<ApiResponse<ProductImageDto>> AddProductImageAsync(Guid productId, AddProductImageDto dto);
    Task<ApiResponse<bool>> DeleteProductImageAsync(Guid imageId);
    Task<ApiResponse<bool>> SetPrimaryImageAsync(Guid productId, Guid imageId);
}