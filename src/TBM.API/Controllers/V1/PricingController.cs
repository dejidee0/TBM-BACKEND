using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TBM.Application.Services.Subscriptions;

namespace TBM.API.Controllers.V1;

[ApiController]
[Route("api/v1/pricing")]
[EnableRateLimiting("DynamicPolicy")]
public class PricingController : ControllerBase
{
    private readonly PricingConfigService _pricingService;

    public PricingController(PricingConfigService pricingService)
    {
        _pricingService = pricingService;
    }

    /// <summary>
    /// Returns all active pricing plans.
    /// Optionally pass ?promoCode=X to see discounted prices.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPricing([FromQuery] string? promoCode = null)
    {
        try
        {
            var result = await _pricingService.GetAllAsync(promoCode);
            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<PricingController>>();
            logger.LogError(ex, "GET /pricing failed");
            return StatusCode(500, new { success = false, message = "Failed to load pricing. Please try again." });
        }
    }
}
