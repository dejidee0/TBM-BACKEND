using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TBM.Application.DTOs.AI;
using TBM.Application.Services;

namespace TBM.API.Controllers.V1.AI;

[ApiController]
[Route("api/v1/ai/renovation")]
[Authorize]
[EnableRateLimiting("DynamicPolicy")]
public class AIRenovationEstimatorController : ControllerBase
{
    private readonly RenovationEstimatorService _estimatorService;

    public AIRenovationEstimatorController(RenovationEstimatorService estimatorService)
    {
        _estimatorService = estimatorService;
    }

    [HttpPost("estimate")]
    [HttpPost("~/api/v1/ai/renovation-estimates")]
    public async Task<IActionResult> CreateEstimate([FromBody] CreateRenovationEstimateRequestDto request)
    {
        try
        {
            var estimate = await _estimatorService.CreateEstimateAsync(GetCurrentUserId(), request);
            return Ok(estimate);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("estimates")]
    [HttpGet("~/api/v1/ai/renovation-estimates")]
    public async Task<IActionResult> GetEstimateHistory()
    {
        var estimates = await _estimatorService.GetEstimatesAsync(GetCurrentUserId());
        return Ok(new { estimates });
    }

    [HttpGet("estimates/{estimateId:guid}")]
    [HttpGet("~/api/v1/ai/renovation-estimates/{estimateId:guid}")]
    public async Task<IActionResult> GetEstimate(Guid estimateId)
    {
        var estimate = await _estimatorService.GetEstimateByIdAsync(GetCurrentUserId(), estimateId);
        if (estimate == null)
        {
            return NotFound(new { success = false, message = "Estimate not found" });
        }

        return Ok(estimate);
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("User ID claim missing.");
        }

        return userId;
    }
}
