using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TBM.Application.DTOs.Products;
using TBM.Application.Interfaces;

namespace TBM.API.Controllers.V1;

[ApiController]
[Route("api/v1/[controller]")]
[EnableRateLimiting("DynamicPolicy")]

public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;
    
    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }
    
    /// <summary>
    /// Get products with filtering and pagination
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] ProductFilterDto filter)
    {
        var result = await _productService.GetProductsAsync(filter);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return Ok(result);
    }
    
    /// <summary>
    /// Get featured products
    /// </summary>
    [HttpGet("featured")]
    public async Task<IActionResult> GetFeatured([FromQuery] int? brandType = null, [FromQuery] int limit = 10)
    {
        var result = await _productService.GetFeaturedProductsAsync(brandType, limit);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return Ok(result);
    }
    
    /// <summary>
    /// Get product by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _productService.GetProductByIdAsync(id);
        
        if (!result.Success)
        {
            return NotFound(result);
        }
        
        return Ok(result);
    }
    
    /// <summary>
    /// Get product by slug
    /// </summary>
    [HttpGet("slug/{slug}")]
    public async Task<IActionResult> GetBySlug(string slug)
    {
        var result = await _productService.GetProductBySlugAsync(slug);
        
        if (!result.Success)
        {
            return NotFound(result);
        }
        
        return Ok(result);
    }
    
    /// <summary>
    /// Get related products
    /// </summary>
    [HttpGet("{id}/related")]
    public async Task<IActionResult> GetRelated(Guid id, [FromQuery] int limit = 4)
    {
        var result = await _productService.GetRelatedProductsAsync(id, limit);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return Ok(result);
    }
    
    /// <summary>
    /// Create a new product (Admin only)
    /// </summary>
    [Authorize(Roles = "SuperAdmin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductDto dto)
    {
        var result = await _productService.CreateProductAsync(dto);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result);
    }
    
    /// <summary>
    /// Update a product (Admin only)
    /// </summary>
    [Authorize(Roles = "SuperAdmin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductDto dto)
    {
        var result = await _productService.UpdateProductAsync(id, dto);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return Ok(result);
    }
    
    /// <summary>
    /// Delete a product (Admin only)
    /// </summary>
    [Authorize(Roles = "SuperAdmin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _productService.DeleteProductAsync(id);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return Ok(result);
    }
    
    /// <summary>
    /// Add image to product (Admin only)
    /// </summary>
    [Authorize(Roles = "SuperAdmin")]
    [HttpPost("{id}/images")]
    public async Task<IActionResult> AddImage(Guid id, [FromBody] AddProductImageDto dto)
    {
        var result = await _productService.AddProductImageAsync(id, dto);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return Ok(result);
    }
    
    /// <summary>
    /// Delete product image (Admin only)
    /// </summary>
    [Authorize(Roles = "SuperAdmin")]
    [HttpDelete("images/{imageId}")]
    public async Task<IActionResult> DeleteImage(Guid imageId)
    {
        var result = await _productService.DeleteProductImageAsync(imageId);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return Ok(result);
    }
    
    /// <summary>
    /// Set primary image for product (Admin only)
    /// </summary>
    [Authorize(Roles = "SuperAdmin")]
    [HttpPut("{productId}/images/{imageId}/primary")]
    public async Task<IActionResult> SetPrimaryImage(Guid productId, Guid imageId)
    {
        var result = await _productService.SetPrimaryImageAsync(productId, imageId);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return Ok(result);
    }
}