using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TBM.Application.DTOs.Portfolio;
using TBM.Application.Services;
using TBM.Core.Enums;
using TBM.Core.Interfaces;

namespace TBM.API.Controllers.V1.Vendor;

/// <summary>Vendor portfolio management — create, edit, and publish showcase projects.</summary>
[ApiController]
[Route("api/v1/vendor/portfolio")]
[Authorize(Roles = "Vendor")]
[EnableRateLimiting("DynamicPolicy")]
public class VendorPortfolioController : ControllerBase
{
    private readonly PortfolioService _portfolioService;
    private readonly IUnitOfWork _unitOfWork;

    public VendorPortfolioController(PortfolioService portfolioService, IUnitOfWork unitOfWork)
    {
        _portfolioService = portfolioService;
        _unitOfWork = unitOfWork;
    }

    /// <summary>Get all portfolio projects for the authenticated vendor.</summary>
    [HttpGet]
    public async Task<IActionResult> GetMine()
    {
        var result = await _portfolioService.GetVendorProjectsAsync(GetVendorId());
        return Ok(result);
    }

    /// <summary>Get a single portfolio project.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _portfolioService.GetVendorProjectByIdAsync(id, GetVendorId());
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    /// <summary>
    /// Create a new portfolio project (metadata only).
    /// Upload images separately via POST /{id}/images.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePortfolioProjectDto dto)
    {
        var vendorId = GetVendorId();
        var user = await _unitOfWork.Users.GetByIdAsync(vendorId);
        var vendorName = user != null ? $"{user.FirstName} {user.LastName}".Trim() : "Vendor";

        var result = await _portfolioService.CreateAsync(vendorId, vendorName, dto);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>Update project metadata (only allowed for Draft or Rejected projects).</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePortfolioProjectDto dto)
    {
        var result = await _portfolioService.UpdateAsync(id, GetVendorId(), dto);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Upload a project image (Before / After / Reference).
    /// Use multipart/form-data. Pass imageType as query param: 0=Before, 1=After, 2=Reference.
    /// </summary>
    [HttpPost("{id:guid}/images")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadImage(
        Guid id,
        IFormFile file,
        [FromQuery] PortfolioImageType imageType = PortfolioImageType.After,
        [FromQuery] string? caption = null)
    {
        var result = await _portfolioService.UploadImageAsync(id, GetVendorId(), file, imageType, caption);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>Delete a specific image from a portfolio project.</summary>
    [HttpDelete("{id:guid}/images/{imageId:guid}")]
    public async Task<IActionResult> DeleteImage(Guid id, Guid imageId)
    {
        var result = await _portfolioService.DeleteImageAsync(id, imageId, GetVendorId());
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>Publish the project to the public showcase. Requires at least one After image.</summary>
    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id)
    {
        var result = await _portfolioService.PublishAsync(id, GetVendorId());
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>Unpublish the project (moves it back to Draft).</summary>
    [HttpPost("{id:guid}/unpublish")]
    public async Task<IActionResult> Unpublish(Guid id)
    {
        var result = await _portfolioService.UnpublishAsync(id, GetVendorId());
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>Delete a portfolio project.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _portfolioService.DeleteAsync(id, GetVendorId());
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    private Guid GetVendorId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(claim) || !Guid.TryParse(claim, out var id))
            throw new UnauthorizedAccessException("User ID not found in token.");
        return id;
    }
}
