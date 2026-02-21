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
    /// Compatibility endpoint for frontend local route: /api/flooring
    /// </summary>
    [HttpGet("~/api/flooring")]
    public async Task<IActionResult> GetFlooring(
        [FromQuery] string? category = null,
        [FromQuery] string? materialType = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        [FromQuery] string? sort = null,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 12)
    {
        var searchParts = new[] { category, materialType }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .ToList();

        var filter = new ProductFilterDto
        {
            PageNumber = page < 1 ? 1 : page,
            PageSize = limit < 1 ? 12 : limit,
            SearchTerm = searchParts.Count > 0 ? string.Join(" ", searchParts) : null,
            ActiveOnly = true
        };

        var result = await _productService.GetProductsAsync(filter);

        if (!result.Success || result.Data == null)
        {
            return BadRequest(result);
        }

        var products = result.Data.Items;

        if (minPrice.HasValue)
        {
            products = products
                .Where(p => p.Price.HasValue && p.Price.Value >= minPrice.Value)
                .ToList();
        }

        if (maxPrice.HasValue)
        {
            products = products
                .Where(p => p.Price.HasValue && p.Price.Value <= maxPrice.Value)
                .ToList();
        }

        products = sort?.Trim().ToLowerInvariant() switch
        {
            "price_asc" => products.OrderBy(p => p.Price ?? decimal.MaxValue).ToList(),
            "price_desc" => products.OrderByDescending(p => p.Price ?? decimal.MinValue).ToList(),
            "newest" => products.OrderByDescending(p => p.CreatedAt).ToList(),
            _ => products
        };

        var total = result.Data.TotalCount;
        var totalPages = (int)Math.Ceiling(total / (double)Math.Max(1, result.Data.PageSize));

        return Ok(new
        {
            products,
            pagination = new
            {
                page = result.Data.PageNumber,
                limit = result.Data.PageSize,
                total,
                totalPages,
                hasMore = result.Data.PageNumber < totalPages
            },
            filters = new
            {
                category,
                materialType,
                minPrice,
                maxPrice,
                sort
            }
        });
    }

    /// <summary>
    /// Compatibility endpoint for frontend route: /materials
    /// </summary>
    [HttpGet("~/materials")]
    public async Task<IActionResult> GetMaterials(
        [FromQuery] string? category = null,
        [FromQuery] string? materialType = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        [FromQuery] string? sort = null,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 12)
    {
        var searchParts = new[] { category, materialType }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .ToList();

        var filter = new ProductFilterDto
        {
            PageNumber = page < 1 ? 1 : page,
            PageSize = limit < 1 ? 12 : limit,
            SearchTerm = searchParts.Count > 0 ? string.Join(" ", searchParts) : null,
            ActiveOnly = true
        };

        var result = await _productService.GetProductsAsync(filter);

        if (!result.Success || result.Data == null)
        {
            return BadRequest(new { success = false, message = result.Message });
        }

        var products = result.Data.Items;

        if (minPrice.HasValue)
        {
            products = products
                .Where(p => p.Price.HasValue && p.Price.Value >= minPrice.Value)
                .ToList();
        }

        if (maxPrice.HasValue)
        {
            products = products
                .Where(p => p.Price.HasValue && p.Price.Value <= maxPrice.Value)
                .ToList();
        }

        products = sort?.Trim().ToLowerInvariant() switch
        {
            "price_asc" => products.OrderBy(p => p.Price ?? decimal.MaxValue).ToList(),
            "price_desc" => products.OrderByDescending(p => p.Price ?? decimal.MinValue).ToList(),
            "newest" => products.OrderByDescending(p => p.CreatedAt).ToList(),
            _ => products
        };

        var total = result.Data.TotalCount;
        var totalPages = (int)Math.Ceiling(total / (double)Math.Max(1, result.Data.PageSize));

        return Ok(new
        {
            materials = products.Select(MapMaterial).ToList(),
            pagination = new
            {
                page = result.Data.PageNumber,
                limit = result.Data.PageSize,
                total,
                totalPages,
                hasMore = result.Data.PageNumber < totalPages
            },
            filters = new
            {
                category,
                materialType,
                minPrice,
                maxPrice,
                sort
            }
        });
    }

    /// <summary>
    /// Compatibility endpoint for frontend route: /materials/:id
    /// </summary>
    [HttpGet("~/materials/{id}")]
    public async Task<IActionResult> GetMaterialById(string id)
    {
        if (Guid.TryParse(id, out var productId))
        {
            var byIdResult = await _productService.GetProductByIdAsync(productId);

            if (!byIdResult.Success || byIdResult.Data == null)
            {
                return NotFound(new { success = false, message = byIdResult.Message });
            }

            return Ok(new { material = MapMaterial(byIdResult.Data) });
        }

        var bySlugResult = await _productService.GetProductBySlugAsync(id);

        if (!bySlugResult.Success || bySlugResult.Data == null)
        {
            return NotFound(new { success = false, message = bySlugResult.Message });
        }

        return Ok(new { material = MapMaterial(bySlugResult.Data) });
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

    private static object MapMaterial(ProductDto product)
    {
        return new
        {
            id = product.Id,
            name = product.Name,
            slug = product.Slug,
            description = product.ShortDescription,
            price = product.Price,
            image = product.PrimaryImageUrl,
            category = product.CategoryName,
            inStock = product.InStock
        };
    }
}
