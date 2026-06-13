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
public class CheckoutController : ControllerBase
{
    private readonly ICheckoutService _checkoutService;

    public CheckoutController(ICheckoutService checkoutService)
    {
        _checkoutService = checkoutService;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetCheckout([FromQuery] string? promoCode = null)
    {
        var userId = GetUserIdOrNull();
        var result = await _checkoutService.GetCheckoutSummaryAsync(userId, Request.Cookies["tbm_guest_id"], promoCode);

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

    [AllowAnonymous]
    [HttpPost("validate-promo")]
    public async Task<IActionResult> ValidatePromo([FromBody] PromoValidationRequestDto dto)
    {
        var userId = GetUserIdOrNull();
        var result = await _checkoutService.ValidatePromoAsync(userId, Request.Cookies["tbm_guest_id"], dto.Code);

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

    [AllowAnonymous]
    [HttpPost("payment")]
    public async Task<IActionResult> ProcessPayment([FromBody] CheckoutPaymentRequestDto dto)
    {
        var userId = GetUserIdOrNull();
        dto.GuestSessionId ??= Request.Cookies["tbm_guest_id"];
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
            idempotent = result.Data.IsIdempotent,
            paymentProvider = result.Data.PaymentProvider,
            paymentReference = result.Data.PaymentReference,
            paymentStatus = result.Data.PaymentStatus,
            authorizationUrl = result.Data.AuthorizationUrl,
            accessCode = result.Data.AccessCode,
            publicKey = result.Data.PublicKey
        });
    }

    [Authorize]
    [HttpGet("payment/paystack/verify/{reference}")]
    public async Task<IActionResult> VerifyPaystackPayment([FromRoute] string reference)
    {
        var userId = GetUserId();
        var result = await _checkoutService.VerifyPaystackPaymentAsync(userId, reference);

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
            idempotent = result.Data.IsIdempotent,
            paymentProvider = result.Data.PaymentProvider,
            paymentReference = result.Data.PaymentReference,
            paymentStatus = result.Data.PaymentStatus,
            publicKey = result.Data.PublicKey
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

    private Guid? GetUserIdOrNull()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
