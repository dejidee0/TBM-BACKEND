using Microsoft.AspNetCore.Mvc;
using TBM.Application.DTOs.Subscriptions;
using TBM.Application.Services.Subscriptions;
using TBM.Core.Enums;

namespace TBM.API.Controllers.V1.Admin;

public class AdminPricingController : BaseAdminController
{
    private readonly PricingConfigService _pricingService;

    public AdminPricingController(PricingConfigService pricingService)
    {
        _pricingService = pricingService;
    }

    /// <summary>GET /api/v1/admin/adminpricing — list all pricing configs.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _pricingService.GetAllAsync();
        return Ok(new { success = true, data = result.Plans });
    }

    /// <summary>GET /api/v1/admin/adminpricing/{tier}/{cycle} — get a specific plan.</summary>
    [HttpGet("{tier}/{cycle}")]
    public async Task<IActionResult> GetByTierAndCycle(string tier, string cycle)
    {
        if (!Enum.TryParse<SubscriptionTier>(tier, true, out var parsedTier) ||
            !Enum.TryParse<BillingCycle>(cycle, true, out var parsedCycle))
        {
            return BadRequest(new { success = false, message = "Invalid tier or billing cycle." });
        }

        var config = await _pricingService.GetByTierAndCycleAsync(parsedTier, parsedCycle);
        if (config == null)
        {
            return NotFound(new { success = false, message = "Pricing config not found." });
        }

        return Ok(new { success = true, data = config });
    }

    /// <summary>PUT /api/v1/admin/adminpricing/{tier}/{cycle} — update a plan's pricing and limits.</summary>
    [HttpPut("{tier}/{cycle}")]
    public async Task<IActionResult> Update(string tier, string cycle, [FromBody] UpdatePricingConfigDto dto)
    {
        if (!Enum.TryParse<SubscriptionTier>(tier, true, out var parsedTier) ||
            !Enum.TryParse<BillingCycle>(cycle, true, out var parsedCycle))
        {
            return BadRequest(new { success = false, message = "Invalid tier or billing cycle." });
        }

        var updated = await _pricingService.UpdateAsync(parsedTier, parsedCycle, dto);
        if (updated == null)
        {
            return NotFound(new { success = false, message = "Pricing config not found." });
        }

        return Ok(new { success = true, data = updated });
    }

    /// <summary>POST /api/v1/admin/adminpricing/seed — seed default pricing configs if none exist.</summary>
    [HttpPost("seed")]
    public async Task<IActionResult> Seed()
    {
        await _pricingService.SeedDefaultsAsync();
        return Ok(new { success = true, message = "Default pricing configs seeded." });
    }
}
