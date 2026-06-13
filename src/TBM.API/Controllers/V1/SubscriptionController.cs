using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using TBM.Application.Configuration;
using TBM.Application.DTOs.Subscriptions;
using TBM.Application.Services.Subscriptions;

namespace TBM.API.Controllers.V1;

[ApiController]
[Route("api/v1/subscription")]
[Authorize]
[EnableRateLimiting("DynamicPolicy")]
public class SubscriptionController : ControllerBase
{
    private readonly SubscriptionService _subscriptionService;
    private readonly AppSettings _appSettings;

    public SubscriptionController(SubscriptionService subscriptionService, IOptions<AppSettings> appSettings)
    {
        _subscriptionService = subscriptionService;
        _appSettings = appSettings.Value;
    }

    /// <summary>GET /api/v1/subscription/current — returns the user's active subscription or null.</summary>
    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent()
    {
        try
        {
            var userId = GetUserId();
            var sub = await _subscriptionService.GetCurrentAsync(userId);

            if (sub == null)
            {
                return Ok(new
                {
                    success = true,
                    subscription = (object?)null,
                    tier = "none",
                    message = $"No active subscription. Visit {_appSettings.FrontendBaseUrl}/pricing to subscribe."
                });
            }

            return Ok(new { success = true, subscription = sub, tier = sub.Tier });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<SubscriptionController>>();
            logger.LogError(ex, "GET /subscription/current failed for user {User}",
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            return StatusCode(500, new { success = false, message = "Failed to load subscription. Please try again." });
        }
    }

    /// <summary>POST /api/v1/subscription/subscribe — initiates a new subscription payment.</summary>
    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeToPlanDto dto)
    {
        try
        {
            var userId = GetUserId();
            var result = await _subscriptionService.SubscribeAsync(userId, dto);

            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.Message });
            }

            return Ok(new { success = true, data = result });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<SubscriptionController>>();
            logger.LogError(ex, "POST /subscription/subscribe failed for user {User}",
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            return StatusCode(500, new { success = false, message = "Failed to initialize subscription payment. Please try again." });
        }
    }

    /// <summary>POST /api/v1/subscription/cancel — cancels the active subscription.</summary>
    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel()
    {
        try
        {
            var userId = GetUserId();
            var canceled = await _subscriptionService.CancelAsync(userId);

            if (!canceled)
            {
                return NotFound(new { success = false, message = "No active subscription found." });
            }

            return Ok(new { success = true, message = "Subscription canceled successfully." });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<SubscriptionController>>();
            logger.LogError(ex, "POST /subscription/cancel failed for user {User}",
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            return StatusCode(500, new { success = false, message = "Failed to cancel subscription. Please try again." });
        }
    }

    /// <summary>
    /// POST /api/v1/subscription/activate — verifies a Paystack payment by reference and activates the subscription.
    /// Called by the frontend after Paystack redirects back to the callback URL.
    /// </summary>
    [HttpPost("activate")]
    public async Task<IActionResult> Activate([FromBody] ActivateSubscriptionDto dto)
    {
        try
        {
            var userId = GetUserId();
            var (success, message, sub) = await _subscriptionService.ActivateByReferenceAsync(
                userId,
                dto.Reference,
                dto.Tier,
                dto.Cycle,
                dto.PromoCode);

            if (!success)
            {
                return BadRequest(new { success = false, message });
            }

            return Ok(new { success = true, message, subscription = sub, tier = sub?.Tier });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<SubscriptionController>>();
            logger.LogError(ex, "POST /subscription/activate failed for user {User}",
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            return StatusCode(500, new { success = false, message = "Failed to activate subscription. Please try again." });
        }
    }

    /// <summary>POST /api/v1/subscription/upgrade — cancels current plan and initiates payment for a new tier.</summary>
    [HttpPost("upgrade")]
    public async Task<IActionResult> Upgrade([FromBody] SubscribeToPlanDto dto)
    {
        try
        {
            var userId = GetUserId();
            var result = await _subscriptionService.UpgradeAsync(userId, dto);

            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.Message });
            }

            return Ok(new { success = true, data = result });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<SubscriptionController>>();
            logger.LogError(ex, "POST /subscription/upgrade failed for user {User}",
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            return StatusCode(500, new { success = false, message = "Failed to upgrade subscription. Please try again." });
        }
    }

    /// <summary>POST /api/v1/subscription/renew — initiates a renewal payment for the current plan.</summary>
    [HttpPost("renew")]
    public async Task<IActionResult> Renew([FromQuery] string? callbackUrl = null)
    {
        try
        {
            var userId = GetUserId();
            var result = await _subscriptionService.RenewAsync(userId, callbackUrl);

            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.Message });
            }

            return Ok(new { success = true, data = result });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<SubscriptionController>>();
            logger.LogError(ex, "POST /subscription/renew failed for user {User}",
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            return StatusCode(500, new { success = false, message = "Failed to initialize renewal. Please try again." });
        }
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(claim) || !Guid.TryParse(claim, out var id))
        {
            throw new UnauthorizedAccessException("User ID not found in token.");
        }

        return id;
    }
}
