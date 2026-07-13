using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TBM.Application.Services;

namespace TBM.API.Controllers.V1;

[ApiController]
[Route("api/v1/inspiration")]
[EnableRateLimiting("DynamicPolicy")]
public class InspirationController : ControllerBase
{
    private readonly InspirationService _inspirationService;

    public InspirationController(InspirationService inspirationService)
    {
        _inspirationService = inspirationService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get([FromQuery] string? category, [FromQuery] string? style)
    {
        var items = await _inspirationService.GetAsync(category, style);
        return Ok(new { items });
    }
}
