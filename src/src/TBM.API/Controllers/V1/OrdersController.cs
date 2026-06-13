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
    /// Compatibility endpoint for frontend route: /api/orders/:orderId
    /// </summary>
    [HttpGet("~/api/orders/{orderId:guid}")]
    public async Task<IActionResult> GetOrderCompatibility(Guid orderId)
    {
        var userId = GetUserId();
        var result = await _orderService.GetOrderByIdAsync(orderId, userId);

        if (!result.Success || result.Data == null)
        {
            return NotFound(new { success = false, message = result.Message });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Compatibility endpoint for frontend route: /api/orders
    /// </summary>
    [HttpGet("~/api/orders")]
    public async Task<IActionResult> GetMyOrdersCompatibility()
    {
        var userId = GetUserId();
        var result = await _orderService.GetUserOrdersAsync(userId);

        if (!result.Success || result.Data == null)
        {
            return BadRequest(new { success = false, message = result.Message });
        }

        return Ok(result.Data.Select(o => new
        {
            id = o.Id,
            orderNumber = o.OrderNumber,
            status = o.StatusName,
            paymentStatus = o.PaymentStatusName,
            total = o.Total,
            createdAt = o.CreatedAt
        }));
    }
    
    /// <summary>
    /// Get order by ID (user's own order)
    /// </summary>
    [HttpGet("{orderId:guid}")]
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
    /// Get invoice URL for a user order
    /// </summary>
    [HttpGet("{orderId:guid}/invoice")]
    [HttpGet("~/api/orders/{orderId:guid}/invoice")]
    public async Task<IActionResult> GetOrderInvoice(Guid orderId)
    {
        var userId = GetUserId();
        var result = await _orderService.GetOrderByIdAsync(orderId, userId);

        if (!result.Success || result.Data == null)
        {
            return NotFound(new { success = false, message = result.Message });
        }

        var invoiceUrl = Url.Action(
            action: nameof(GetInvoiceDocument),
            controller: "Orders",
            values: new { orderId },
            protocol: Request.Scheme);

        if (string.IsNullOrWhiteSpace(invoiceUrl))
        {
            invoiceUrl = $"{Request.Scheme}://{Request.Host}/api/v1/orders/{orderId}/invoice/document";
        }

        return Ok(new
        {
            success = true,
            url = invoiceUrl
        });
    }

    /// <summary>
    /// Get invoice data for a user order
    /// </summary>
    [HttpGet("{orderId:guid}/invoice/document")]
    public async Task<IActionResult> GetInvoiceDocument(Guid orderId)
    {
        var userId = GetUserId();
        var result = await _orderService.GetOrderByIdAsync(orderId, userId);

        if (!result.Success || result.Data == null)
        {
            return NotFound(new { success = false, message = result.Message });
        }

        var order = result.Data;

        return Ok(new
        {
            invoiceNumber = $"INV-{order.OrderNumber}",
            orderNumber = order.OrderNumber,
            issuedAt = DateTime.UtcNow,
            customer = new
            {
                name = order.UserFullName,
                email = order.UserEmail,
                phone = order.ShippingPhone
            },
            shippingAddress = new
            {
                fullName = order.ShippingFullName,
                address = order.ShippingAddress,
                city = order.ShippingCity,
                state = order.ShippingState
            },
            items = order.Items.Select(i => new
            {
                productId = i.ProductId,
                name = i.ProductName,
                sku = i.ProductSKU,
                quantity = i.Quantity,
                unitPrice = i.UnitPrice,
                subTotal = i.SubTotal
            }),
            totals = new
            {
                subTotal = order.SubTotal,
                shipping = order.ShippingCost,
                tax = order.Tax,
                discount = order.Discount,
                total = order.Total
            },
            payment = new
            {
                status = order.PaymentStatusName,
                method = order.PaymentMethodName,
                reference = order.PaymentReference,
                paidAt = order.PaidAt
            }
        });
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
