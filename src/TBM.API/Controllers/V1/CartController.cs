using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    
    public CartController(ICartService cartService)
    {
        _cartService = cartService;
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