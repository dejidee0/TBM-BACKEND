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
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    
    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }
    
    /// <summary>
    /// Get current user's orders
    /// </summary>
    [HttpGet("my-orders")]
    public async Task<IActionResult> GetMyOrders()
    {
        var userId = GetUserId();
        var result = await _orderService.GetUserOrdersAsync(userId);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return Ok(result);
    }
    
    /// <summary>
    /// Get order by ID (user's own order)
    /// </summary>
    [HttpGet("{orderId}")]
    public async Task<IActionResult> GetOrder(Guid orderId)
    {
        var userId = GetUserId();
        var result = await _orderService.GetOrderByIdAsync(orderId, userId);
        
        if (!result.Success)
        {
            return NotFound(result);
        }
        
        return Ok(result);
    }
    
    /// <summary>
    /// Get order by order number
    /// </summary>
    [HttpGet("number/{orderNumber}")]
    public async Task<IActionResult> GetOrderByNumber(string orderNumber)
    {
        var userId = GetUserId();
        var result = await _orderService.GetOrderByNumberAsync(orderNumber, userId);
        
        if (!result.Success)
        {
            return NotFound(result);
        }
        
        return Ok(result);
    }
    
    /// <summary>
    /// Create order from cart
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto dto)
    {
        var userId = GetUserId();
        var result = await _orderService.CreateOrderAsync(userId, dto);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return CreatedAtAction(nameof(GetOrder), new { orderId = result.Data!.Id }, result);
    }
    
    /// <summary>
    /// Cancel order (customer)
    /// </summary>
    [HttpPost("{orderId}/cancel")]
    public async Task<IActionResult> CancelOrder(Guid orderId, [FromBody] CancelOrderDto dto)
    {
        var userId = GetUserId();
        var result = await _orderService.CancelOrderAsync(orderId, userId, dto.Reason);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return Ok(result);
    }
    
    /// <summary>
    /// Get all orders with filters (Admin only)
    /// </summary>
    [Authorize(Roles = "SuperAdmin")]
    [HttpGet]
    public async Task<IActionResult> GetAllOrders([FromQuery] OrderFilterDto filter)
    {
        var result = await _orderService.GetOrdersAsync(filter);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return Ok(result);
    }
    
    /// <summary>
    /// Get order by ID (Admin - any order)
    /// </summary>
    [Authorize(Roles = "SuperAdmin")]
    [HttpGet("admin/{orderId}")]
    public async Task<IActionResult> GetOrderAdmin(Guid orderId)
    {
        var result = await _orderService.GetOrderByIdAsync(orderId);
        
        if (!result.Success)
        {
            return NotFound(result);
        }
        
        return Ok(result);
    }
    
    /// <summary>
    /// Update order status (Admin only)
    /// </summary>
    [Authorize(Roles = "SuperAdmin")]
    [HttpPut("{orderId}/status")]
    public async Task<IActionResult> UpdateOrderStatus(Guid orderId, [FromBody] UpdateOrderStatusDto dto)
    {
        var result = await _orderService.UpdateOrderStatusAsync(orderId, dto);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return Ok(result);
    }
    
    /// <summary>
    /// Update payment status (Admin only)
    /// </summary>
    [Authorize(Roles = "SuperAdmin")]
    [HttpPut("{orderId}/payment")]
    public async Task<IActionResult> UpdatePaymentStatus(Guid orderId, [FromBody] UpdatePaymentStatusDto dto)
    {
        var result = await _orderService.UpdatePaymentStatusAsync(orderId, dto);
        
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