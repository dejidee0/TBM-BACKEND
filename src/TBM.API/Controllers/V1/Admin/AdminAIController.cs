using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TBM.Application.DTOs.AI;
using TBM.Application.Services;

namespace TBM.API.Controllers.V1.Admin;

[ApiController]
[Route("api/admin/ai")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminAIController : ControllerBase
{
    private readonly AICreditService _aiCreditService;
    private readonly AIUsageService _aiUsageService;

    public AdminAIController(
        AICreditService aiCreditService,
        AIUsageService aiUsageService)
    {
        _aiCreditService = aiCreditService;
        _aiUsageService = aiUsageService;
    }

    [HttpGet("credits/{userId:guid}")]
    public async Task<IActionResult> GetUserCreditBalance(Guid userId)
    {
        var balance = await _aiCreditService.GetBalanceAsync(userId);
        return Ok(balance);
    }

    [HttpPost("credits/adjust")]
    public async Task<IActionResult> AdjustCredits([FromBody] AICreditAdjustmentRequestDto request)
    {
        try
        {
            var adminUserId = GetCurrentUserId();
            var result = await _aiCreditService.AdjustByAdminAsync(
                adminUserId,
                request.UserId,
                request.Amount,
                request.Reason);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("usage/monthly-spend")]
    public async Task<IActionResult> GetMonthlySpend([FromQuery] int months = 6)
    {
        var report = await _aiUsageService.GetMonthlySpendAsync(months);
        return Ok(report);
    }

    [HttpGet("usage/user/{userId:guid}")]
    public async Task<IActionResult> GetUserUsageSummary(
        Guid userId,
        [FromQuery] int? year = null,
        [FromQuery] int? month = null)
    {
        var summary = await _aiUsageService.GetUserSummaryAsync(userId, year, month);
        return Ok(summary);
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(claim) || !Guid.TryParse(claim, out var id))
        {
            throw new UnauthorizedAccessException("User ID claim missing.");
        }

        return id;
    }
}
