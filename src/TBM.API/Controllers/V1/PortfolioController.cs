using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TBM.Application.Services;

namespace TBM.API.Controllers.V1;

/// <summary>Public showcase — anyone can browse published vendor portfolio projects.</summary>
[ApiController]
[Route("api/v1/portfolio")]
[EnableRateLimiting("DynamicPolicy")]
public class PortfolioController : ControllerBase
{
    private readonly PortfolioService _portfolioService;

    public PortfolioController(PortfolioService portfolioService)
    {
        _portfolioService = portfolioService;
    }

    /// <summary>Browse all published portfolio projects with optional filters.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? category = null,
        [FromQuery] string? location = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12)
    {
        var result = await _portfolioService.GetPublishedAsync(category, location, page, pageSize);
        return Ok(result);
    }

    /// <summary>Get a single published portfolio project.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _portfolioService.GetPublishedByIdAsync(id);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }
}
