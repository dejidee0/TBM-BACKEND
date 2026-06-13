using Microsoft.AspNetCore.Mvc;
using TBM.Application.DTOs.Portfolio;
using TBM.Application.Services;
using TBM.Core.Enums;

namespace TBM.API.Controllers.V1.Admin;

/// <summary>Admin: create, review, and manage all vendor portfolio projects.</summary>
public class AdminPortfolioController : BaseAdminController
{
    private readonly PortfolioService _portfolioService;

    public AdminPortfolioController(PortfolioService portfolioService)
    {
        _portfolioService = portfolioService;
    }

    /// <summary>List all portfolio projects with optional status filter.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] PortfolioStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _portfolioService.AdminGetAllAsync(status, page, pageSize);
        return Ok(result);
    }

    /// <summary>Get a single portfolio project by ID (any status).</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _portfolioService.AdminGetByIdAsync(id);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    /// <summary>
    /// Create a portfolio project on behalf of a vendor.
    /// Set PublishImmediately=true to go live without a separate publish call.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AdminCreatePortfolioProjectDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(new { success = false, message = "Title is required." });

        var result = await _portfolioService.AdminCreateAsync(dto);
        return Ok(result);
    }

    /// <summary>
    /// Upload an image to a portfolio project.
    /// imageType query param: 0 = Before, 1 = After, 2 = Reference.
    /// Use multipart/form-data.
    /// </summary>
    [HttpPost("{id:guid}/images")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadImage(
        Guid id,
        IFormFile file,
        [FromQuery] PortfolioImageType imageType = PortfolioImageType.After,
        [FromQuery] string? caption = null)
    {
        var result = await _portfolioService.AdminUploadImageAsync(id, file, imageType, caption);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>Publish a portfolio project (bypasses the After-image requirement).</summary>
    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id)
    {
        var result = await _portfolioService.AdminPublishAsync(id);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>Update portfolio project status (Publish / Reject) with optional rejection reason.</summary>
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdatePortfolioStatusDto dto)
    {
        var result = await _portfolioService.AdminUpdateStatusAsync(id, dto);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }
}
