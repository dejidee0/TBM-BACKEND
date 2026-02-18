using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TBM.Application.DTOs.Products;
using TBM.Application.Interfaces;

namespace TBM.API.Controllers.V1;

[ApiController]
[Route("api/v1/[controller]")]
[EnableRateLimiting("DynamicPolicy")]

public class CategoriesController : ControllerBase
{
    private readonly IProductService _productService;
    
    public CategoriesController(IProductService productService)
    {
        _productService = productService;
    }
    
    /// <summary>
    /// Get all categories
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _productService.GetAllCategoriesAsync();
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return Ok(result);
    }
    
    /// <summary>
    /// Get categories by brand (1=TBM, 2=Bogat)
    /// </summary>
    [HttpGet("brand/{brandType}")]
    public async Task<IActionResult> GetByBrand(int brandType)
    {
        var result = await _productService.GetCategoriesByBrandAsync(brandType);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return Ok(result);
    }
    
    /// <summary>
    /// Get category by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _productService.GetCategoryByIdAsync(id);
        
        if (!result.Success)
        {
            return NotFound(result);
        }
        
        return Ok(result);
    }
    
    /// <summary>
    /// Get category by slug
    /// </summary>
    [HttpGet("slug/{slug}")]
    public async Task<IActionResult> GetBySlug(string slug)
    {
        var result = await _productService.GetCategoryBySlugAsync(slug);
        
        if (!result.Success)
        {
            return NotFound(result);
        }
        
        return Ok(result);
    }
    
    /// <summary>
    /// Create a new category (Admin only)
    /// </summary>
    [Authorize(Roles = "SuperAdmin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCategoryDto dto)
    {
        var result = await _productService.CreateCategoryAsync(dto);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result);
    }
    
    /// <summary>
    /// Update a category (Admin only)
    /// </summary>
    [Authorize(Roles = "SuperAdmin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCategoryDto dto)
    {
        var result = await _productService.UpdateCategoryAsync(id, dto);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return Ok(result);
    }
    
    /// <summary>
    /// Delete a category (Admin only)
    /// </summary>
    [Authorize(Roles = "SuperAdmin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _productService.DeleteCategoryAsync(id);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return Ok(result);
    }
}