using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TBM.Application.DTOs.AI;
using TBM.Application.Services;

namespace TBM.API.Controllers.V1.AI;

[ApiController]
[Route("api/v1/ai")]
[Authorize]
public class AIController : ControllerBase
{
    private readonly AIService _aiService;
    private readonly AIUsageService _aiUsageService;
    private readonly AICreditService _aiCreditService;

    public AIController(
        AIService aiService,
        AIUsageService aiUsageService,
        AICreditService aiCreditService)
    {
        _aiService = aiService;
        _aiUsageService = aiUsageService;
        _aiCreditService = aiCreditService;
    }

    [HttpPost("projects")]
    public async Task<IActionResult> CreateProject([FromBody] CreateAIProjectDto dto)
    {
        var userId = GetCurrentUserId();
        var project = await _aiService.CreateProjectAsync(userId, dto);
        return Ok(project);
    }

    [HttpPost("generate/image")]
    public async Task<IActionResult> GenerateImage([FromBody] GenerateImageDto dto)
    {
        return await ExecuteGenerationAsync(() => _aiService.GenerateImageAsync(GetCurrentUserId(), dto));
    }

    [HttpPost("transform/image")]
    public async Task<IActionResult> TransformImage([FromBody] GenerateImageDto dto)
    {
        return await ExecuteGenerationAsync(() => _aiService.GenerateImageAsync(GetCurrentUserId(), dto));
    }

    [HttpPost("generate/video")]
    public async Task<IActionResult> GenerateVideo([FromBody] GenerateVideoDto dto)
    {
        return await ExecuteGenerationAsync(() => _aiService.GenerateVideoAsync(GetCurrentUserId(), dto));
    }

    [HttpGet("usage/summary")]
    public async Task<IActionResult> GetUsageSummary([FromQuery] int? year = null, [FromQuery] int? month = null)
    {
        var userId = GetCurrentUserId();
        var summary = await _aiUsageService.GetUserSummaryAsync(userId, year, month);
        return Ok(summary);
    }

    [HttpGet("credits/balance")]
    public async Task<IActionResult> GetCreditBalance()
    {
        var userId = GetCurrentUserId();
        var balance = await _aiCreditService.GetBalanceAsync(userId);
        return Ok(balance);
    }

    private async Task<IActionResult> ExecuteGenerationAsync<T>(Func<Task<T>> action)
    {
        try
        {
            var result = await action();
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
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
