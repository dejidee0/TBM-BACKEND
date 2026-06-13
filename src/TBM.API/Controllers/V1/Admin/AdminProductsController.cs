using Microsoft.AspNetCore.Mvc;
using TBM.Application.DTOs.Products;
using TBM.Application.Interfaces;

namespace TBM.API.Controllers.V1.Admin;

/// <summary>
/// Admin endpoints for managing the Bogat product inventory.
/// All routes require SuperAdmin or Admin role (inherited from BaseAdminController).
/// </summary>
public class AdminProductsController : BaseAdminController
{
    private readonly IProductService _productService;

    public AdminProductsController(IProductService productService)
    {
        _productService = productService;
    }

    /// <summary>
    /// Bulk-upload multiple Bogat inventory products in a single request.
    /// Accepts a JSON array of products. Each item is validated individually —
    /// failures are reported per-item without rolling back the successful ones.
    /// </summary>
    /// <remarks>
    /// POST /api/v1/admin/adminproducts/bulk
    ///
    /// Example body:
    /// [
    ///   {
    ///     "name": "Oak Engineered Flooring",
    ///     "description": "Premium wide-plank oak flooring, UV-lacquered finish",
    ///     "shortDescription": "Wide-plank oak flooring",
    ///     "sku": "BOG-FLR-OAK-001",
    ///     "brandType": 1,
    ///     "productType": 1,
    ///     "categoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    ///     "price": 45000,
    ///     "stockQuantity": 200,
    ///     "trackInventory": true,
    ///     "isFeatured": true,
    ///     "materialType": "Wood",
    ///     "aiKeywords": "oak flooring hardwood plank wood floor",
    ///     "recommendedFor": "living room bedroom hallway",
    ///     "qualityTier": "Premium"
    ///   }
    /// ]
    /// </remarks>
    [HttpPost("bulk")]
    public async Task<IActionResult> BulkCreate([FromBody] List<CreateProductDto> products)
    {
        if (products == null || products.Count == 0)
            return BadRequest(new { success = false, message = "Product list cannot be empty." });

        if (products.Count > 500)
            return BadRequest(new { success = false, message = "Maximum 500 products per bulk upload." });

        var result = await _productService.BulkCreateProductsAsync(products);
        return Ok(result);
    }

    /// <summary>
    /// Create a single Bogat inventory product.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductDto dto)
    {
        var result = await _productService.CreateProductAsync(dto);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Update a Bogat inventory product.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductDto dto)
    {
        var result = await _productService.UpdateProductAsync(id, dto);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Delete a Bogat inventory product (soft delete).
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _productService.DeleteProductAsync(id);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Add an image to a product.
    /// </summary>
    [HttpPost("{id:guid}/images")]
    public async Task<IActionResult> AddImage(Guid id, [FromBody] AddProductImageDto dto)
    {
        var result = await _productService.AddProductImageAsync(id, dto);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Delete a product image.
    /// </summary>
    [HttpDelete("images/{imageId:guid}")]
    public async Task<IActionResult> DeleteImage(Guid imageId)
    {
        var result = await _productService.DeleteProductImageAsync(imageId);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Set the primary display image for a product.
    /// </summary>
    [HttpPut("{productId:guid}/images/{imageId:guid}/primary")]
    public async Task<IActionResult> SetPrimaryImage(Guid productId, Guid imageId)
    {
        var result = await _productService.SetPrimaryImageAsync(productId, imageId);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }
}
