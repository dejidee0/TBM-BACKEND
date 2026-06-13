using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TBM.Application.DTOs.Checkout;
using TBM.Application.Interfaces;

namespace TBM.API.Controllers.V1;

[ApiController]
[Route("api/v1/[controller]")]
[EnableRateLimiting("DynamicPolicy")]
[Authorize]
public class CheckoutController : ControllerBase
{
    private readonly ICheckoutService _checkoutService;

    public CheckoutController(ICheckoutService checkoutService)
    {
        _checkoutService = checkoutService;
    }

    [HttpGet]
    [HttpGet("~/api/checkout")]
    public async Task<IActionResult> GetCheckout([FromQuery] string? promoCode = null)
    {
        var userId = GetUserId();
        var result = await _checkoutService.GetCheckoutSummaryAsync(userId, promoCode);

        if (!result.Success || result.Data == null)
        {
            return BadRequest(new
            {
                success = false,
                message = result.Message,
                errors = result.Errors
            });
        }

        return Ok(new
        {
            items = result.Data.Items,
            subtotal = result.Data.Subtotal,
            shipping = result.Data.Shipping,
            tax = result.Data.Tax,
            discount = result.Data.Discount,
            total = result.Data.Total,
            savedAddresses = result.Data.SavedAddresses,
            defaultAddress = result.Data.DefaultAddress
        });
    }

    [HttpPost("validate-promo")]
    [HttpPost("~/api/checkout/validate-promo")]
    public async Task<IActionResult> ValidatePromo([FromBody] PromoValidationRequestDto dto)
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
            discountAmount = result.Data.DiscountAmount,
            message = result.Data.Message
        });
    }

    [HttpPost("payment")]
    [HttpPost("~/api/checkout/payment")]
    public async Task<IActionResult> ProcessPayment([FromBody] CheckoutPaymentRequestDto dto)
    {
        var userId = GetUserId();
        var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault()
            ?? Request.Headers["X-Idempotency-Key"].FirstOrDefault();

        var result = await _checkoutService.ProcessPaymentAsync(userId, dto, idempotencyKey);

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
            orderId = result.Data.OrderId,
            orderNumber = result.Data.OrderNumber,
            message = result.Data.Message,
            idempotent = result.Data.IsIdempotent
        });
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
