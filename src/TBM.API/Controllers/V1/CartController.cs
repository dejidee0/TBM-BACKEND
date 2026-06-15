using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TBM.Application.DTOs.Common;
using TBM.Application.DTOs.Checkout;
using TBM.Application.DTOs.Orders;
using TBM.Application.Interfaces;

namespace TBM.API.Controllers.V1;

[ApiController]
[Route("api/v1/[controller]")]
[EnableRateLimiting("DynamicPolicy")]
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
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetCart()
    {
        var result = await GetCurrentCartAsync();
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return Ok(result);
    }

    /// <summary>
    /// Compatibility endpoint for frontend route: /api/cart
    /// </summary>
    [ApiExplorerSettings(IgnoreApi = true)]
    [AllowAnonymous]
    [HttpGet("api/cart")]
    public async Task<IActionResult> GetCartCompatibility()
    {
        var result = await GetCurrentCartAsync();

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
    [AllowAnonymous]
    [HttpPost("items")]
    public async Task<IActionResult> AddToCart([FromBody] AddToCartDto dto)
    {
        var result = await AddToCurrentCartAsync(dto);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return Ok(result);
    }

    /// <summary>
    /// Compatibility endpoint for frontend route: /api/cart/add
    /// </summary>
    [ApiExplorerSettings(IgnoreApi = true)]
    [AllowAnonymous]
    [HttpPost("api/cart/add")]
    public async Task<IActionResult> AddToCartCompatibility([FromBody] AddToCartDto dto)
    {
        var result = await AddToCurrentCartAsync(dto);

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
    [AllowAnonymous]
    [HttpPut("items/{itemId}")]
    public async Task<IActionResult> UpdateCartItem(Guid itemId, [FromBody] UpdateCartItemDto dto)
    {
        var result = await UpdateCurrentCartItemAsync(itemId, dto);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return Ok(result);
    }

    /// <summary>
    /// Compatibility endpoint for frontend route: /api/cart/items/:itemId
    /// </summary>
    [ApiExplorerSettings(IgnoreApi = true)]
    [AllowAnonymous]
    [HttpPut("api/cart/items/{itemId:guid}")]
    public async Task<IActionResult> UpdateCartItemCompatibility(Guid itemId, [FromBody] UpdateCartItemDto dto)
    {
        var result = await UpdateCurrentCartItemAsync(itemId, dto);

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
    [AllowAnonymous]
    [HttpDelete("items/{itemId}")]
    public async Task<IActionResult> RemoveCartItem(Guid itemId)
    {
        var result = await RemoveCurrentCartItemAsync(itemId);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return Ok(result);
    }

    /// <summary>
    /// Compatibility endpoint for frontend route: /api/cart/items/:itemId
    /// </summary>
    [ApiExplorerSettings(IgnoreApi = true)]
    [AllowAnonymous]
    [HttpDelete("api/cart/items/{itemId:guid}")]
    public async Task<IActionResult> RemoveCartItemCompatibility(Guid itemId)
    {
        var result = await RemoveCurrentCartItemAsync(itemId);

        if (!result.Success)
        {
            return BadRequest(new { success = false, message = result.Message });
        }

        return Ok(new { success = true });
    }
    
    /// <summary>
    /// Clear all items from cart
    /// </summary>
    [AllowAnonymous]
    [HttpDelete]
    public async Task<IActionResult> ClearCart()
    {
        var result = await ClearCurrentCartAsync();
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return Ok(result);
    }

    /// <summary>
    /// Merge guest cart items into authenticated user's persistent cart
    /// </summary>
    [Authorize]
    [HttpPost("merge")]
    public async Task<IActionResult> MergeCart([FromBody] MergeCartRequestDto dto)
    {
        var userId = GetUserId();
        var result = await _cartService.MergeGuestCartAsync(userId, dto);

        if (!result.Success || result.Data == null)
        {
            return BadRequest(new { success = false, message = result.Message });
        }

        return Ok(new
        {
            success = true,
            cart = result.Data.Cart,
            warnings = result.Data.Warnings
        });
    }

    /// <summary>
    /// Compatibility endpoint for frontend route: /api/cart/merge
    /// </summary>
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpPost("~/api/v1/cart/merge")]
    public Task<IActionResult> MergeCartCompatibility([FromBody] MergeCartRequestDto dto)
    {
        return MergeCart(dto);
    }

    /// <summary>
    /// Apply promo code to current cart
    /// </summary>
    [Authorize]
    [HttpPost("apply-promo")]
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
    [AllowAnonymous]
    [HttpGet("related")]
    public async Task<IActionResult> GetCartRelated([FromQuery] int limit = 4)
    {
        var cartResult = await GetCurrentCartAsync();

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
    
    private async Task<ApiResponse<CartDto>> GetCurrentCartAsync()
    {
        var userId = GetUserIdOrNull();
        return userId.HasValue
            ? await _cartService.GetCartAsync(userId.Value)
            : await _cartService.GetGuestCartAsync(GetGuestSessionId()!);
    }

    private async Task<ApiResponse<CartDto>> AddToCurrentCartAsync(AddToCartDto dto)
    {
        var userId = GetUserIdOrNull();
        return userId.HasValue
            ? await _cartService.AddToCartAsync(userId.Value, dto)
            : await _cartService.AddToGuestCartAsync(GetGuestSessionId()!, dto);
    }

    private async Task<ApiResponse<CartDto>> UpdateCurrentCartItemAsync(Guid itemId, UpdateCartItemDto dto)
    {
        var userId = GetUserIdOrNull();
        return userId.HasValue
            ? await _cartService.UpdateCartItemAsync(userId.Value, itemId, dto)
            : await _cartService.UpdateGuestCartItemAsync(GetGuestSessionId()!, itemId, dto.Quantity);
    }

    private async Task<ApiResponse<bool>> RemoveCurrentCartItemAsync(Guid itemId)
    {
        var userId = GetUserIdOrNull();
        return userId.HasValue
            ? await _cartService.RemoveCartItemAsync(userId.Value, itemId)
            : await _cartService.RemoveGuestCartItemAsync(GetGuestSessionId()!, itemId);
    }

    private async Task<ApiResponse<bool>> ClearCurrentCartAsync()
    {
        var userId = GetUserIdOrNull();
        return userId.HasValue
            ? await _cartService.ClearCartAsync(userId.Value)
            : await _cartService.ClearGuestCartAsync(GetGuestSessionId()!);
    }

    private string? GetGuestSessionId()
    {
        var existing = Request.Cookies["tbm_guest_id"];
        if (!string.IsNullOrEmpty(existing)) return existing;

        var newId = Guid.NewGuid().ToString();
        Response.Cookies.Append("tbm_guest_id", newId, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        });

        return newId;
    }

    private Guid? GetUserIdOrNull()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
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
