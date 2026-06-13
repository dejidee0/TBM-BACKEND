using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TBM.Application.DTOs.Checkout;
using TBM.Application.DTOs.Orders;
using TBM.Application.Interfaces;

namespace TBM.API.Controllers.V1;

[ApiController]
[Route("api/v1/[controller]")]
[EnableRateLimiting("DynamicPolicy")]

[Authorize]
public class CartController : ControllerBase
{
    private readonly ICartService _cartService;
    private readonly ICheckoutService _checkoutService;
    private readonly IProductService _productService;
    
    public CartController(
        ICartService cartService,
        ICheckoutService checkoutService,
        IProductService productService)
    {
        _cartService = cartService;
        _checkoutService = checkoutService;
        _productService = productService;
    }
    
    /// <summary>
    /// Get current user's cart
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCart()
    {
        var userId = GetUserId();
        var result = await _cartService.GetCartAsync(userId);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return Ok(result);
    }

    /// <summary>
    /// Compatibility endpoint for frontend route: /api/cart
    /// </summary>
    [HttpGet("~/api/cart")]
    public async Task<IActionResult> GetCartCompatibility()
    {
        var userId = GetUserId();
        var result = await _cartService.GetCartAsync(userId);

        if (!result.Success || result.Data == null)
        {
            return BadRequest(new { success = false, message = result.Message });
        }

        var shipping = result.Data.SubTotal >= 500000m ? 0m : 5000m;

        return Ok(new
        {
            items = result.Data.Items,
            subtotal = result.Data.SubTotal,
            shipping,
            taxRate = 0.075m,
            estimatedDelivery = DateTime.UtcNow.AddDays(3).ToString("yyyy-MM-dd")
        });
    }
    
    /// <summary>
    /// Add item to cart
    /// </summary>
    [HttpPost("items")]
    public async Task<IActionResult> AddToCart([FromBody] AddToCartDto dto)
    {
        var userId = GetUserId();
        var result = await _cartService.AddToCartAsync(userId, dto);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return Ok(result);
    }

    /// <summary>
    /// Compatibility endpoint for frontend route: /api/cart/add
    /// </summary>
    [HttpPost("~/api/cart/add")]
    public async Task<IActionResult> AddToCartCompatibility([FromBody] AddToCartDto dto)
    {
        var userId = GetUserId();
        var result = await _cartService.AddToCartAsync(userId, dto);

        if (!result.Success || result.Data == null)
        {
            return BadRequest(new { success = false, message = result.Message });
        }

        var item = result.Data.Items
            .Where(i => i.ProductId == dto.ProductId)
            .OrderByDescending(i => i.AddedAt)
            .FirstOrDefault();

        return Ok(new
        {
            success = true,
            item,
            message = result.Message
        });
    }
    
    /// <summary>
    /// Update cart item quantity
    /// </summary>
    [HttpPut("items/{itemId}")]
    public async Task<IActionResult> UpdateCartItem(Guid itemId, [FromBody] UpdateCartItemDto dto)
    {
        var userId = GetUserId();
        var result = await _cartService.UpdateCartItemAsync(userId, itemId, dto);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return Ok(result);
    }

    /// <summary>
    /// Compatibility endpoint for frontend route: /api/cart/items/:itemId
    /// </summary>
    [HttpPut("~/api/cart/items/{itemId:guid}")]
    public async Task<IActionResult> UpdateCartItemCompatibility(Guid itemId, [FromBody] UpdateCartItemDto dto)
    {
        var userId = GetUserId();
        var result = await _cartService.UpdateCartItemAsync(userId, itemId, dto);

        if (!result.Success || result.Data == null)
        {
            return BadRequest(new { success = false, message = result.Message });
        }

        var item = result.Data.Items.FirstOrDefault(i => i.Id == itemId);

        return Ok(new
        {
            success = true,
            item
        });
    }
    
    /// <summary>
    /// Remove item from cart
    /// </summary>
    [HttpDelete("items/{itemId}")]
    public async Task<IActionResult> RemoveCartItem(Guid itemId)
    {
        var userId = GetUserId();
        var result = await _cartService.RemoveCartItemAsync(userId, itemId);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return Ok(result);
    }

    /// <summary>
    /// Compatibility endpoint for frontend route: /api/cart/items/:itemId
    /// </summary>
    [HttpDelete("~/api/cart/items/{itemId:guid}")]
    public async Task<IActionResult> RemoveCartItemCompatibility(Guid itemId)
    {
        var userId = GetUserId();
        var result = await _cartService.RemoveCartItemAsync(userId, itemId);

        if (!result.Success)
        {
            return BadRequest(new { success = false, message = result.Message });
        }

        return Ok(new { success = true });
    }
    
    /// <summary>
    /// Clear all items from cart
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> ClearCart()
    {
        var userId = GetUserId();
        var result = await _cartService.ClearCartAsync(userId);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return Ok(result);
    }

    /// <summary>
    /// Apply promo code to current cart
    /// </summary>
    [HttpPost("~/api/cart/apply-promo")]
    public async Task<IActionResult> ApplyPromo([FromBody] PromoValidationRequestDto dto)
    {
        var userId = GetUserId();
        var result = await _checkoutService.ValidatePromoAsync(userId, dto.Code);

        if (!result.Success || result.Data == null)
        {
            return BadRequest(new
            {
                success = false,
                message = result.Message
            });
        }

        return Ok(new
        {
            success = true,
            code = result.Data.Code,
            discount = result.Data.Discount,
            type = result.Data.Type,
            discountAmount = result.Data.DiscountAmount
        });
    }

    /// <summary>
    /// Get related products based on items currently in cart
    /// </summary>
    [HttpGet("~/api/cart/related")]
    public async Task<IActionResult> GetCartRelated([FromQuery] int limit = 4)
    {
        var userId = GetUserId();
        var cartResult = await _cartService.GetCartAsync(userId);

        if (!cartResult.Success || cartResult.Data == null)
        {
            return BadRequest(new { success = false, message = cartResult.Message });
        }

        var anchorProductId = cartResult.Data.Items.FirstOrDefault()?.ProductId;
        var relatedResult = anchorProductId.HasValue
            ? await _productService.GetRelatedProductsAsync(anchorProductId.Value, limit < 1 ? 4 : limit)
            : await _productService.GetFeaturedProductsAsync(limit: limit < 1 ? 4 : limit);

        if (!relatedResult.Success || relatedResult.Data == null)
        {
            return BadRequest(new { success = false, message = relatedResult.Message });
        }

        var payload = relatedResult.Data.Select(product => new
        {
            id = product.Id,
            name = product.Name,
            price = product.Price ?? 0m,
            image = product.PrimaryImageUrl,
            rating = 4.5m
        });

        return Ok(payload);
    }
    
    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("User ID not found in token");
        }
        
        return userId;
    }
}
