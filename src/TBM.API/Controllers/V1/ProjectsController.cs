using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TBM.Application.DTOs.DesignFlow;
using TBM.Application.Services.DesignFlow;

namespace TBM.API.Controllers.V1;

[ApiController]
[Route("api/v1/projects")]
[EnableRateLimiting("DynamicPolicy")]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly ProjectService _projectService;

    public ProjectsController(ProjectService projectService)
    {
        _projectService = projectService;
    }

    [HttpGet]
    public async Task<IActionResult> GetProjects()
    {
        var userId = GetUserId();
        var result = await _projectService.GetUserProjectsAsync(userId);

        if (!result.Success || result.Data == null)
        {
            return BadRequest(result);
        }

        return Ok(new
        {
            projects = result.Data.Projects
        });
    }

    [HttpGet("{projectId:guid}")]
    public async Task<IActionResult> GetProject([FromRoute] Guid projectId)
    {
        var userId = GetUserId();
        var result = await _projectService.GetProjectDetailsAsync(projectId, userId);

        if (!result.Success || result.Data == null)
        {
            return NotFound(result);
        }

        return Ok(new
        {
            project = result.Data.Project,
            timeline = result.Data.Timeline,
            materials = result.Data.Materials,
            financial = result.Data.Financial,
            vendor = result.Data.Vendor
        });
    }

    [HttpGet("{projectId:guid}/timeline")]
    public async Task<IActionResult> GetTimeline([FromRoute] Guid projectId)
    {
        var userId = GetUserId();
        var result = await _projectService.GetProjectTimelineAsync(projectId, userId);

        if (!result.Success || result.Data == null)
        {
            return NotFound(result);
        }

        return Ok(new
        {
            milestones = result.Data
        });
    }

    [HttpGet("{projectId:guid}/documents")]
    public async Task<IActionResult> GetDocuments([FromRoute] Guid projectId)
    {
        var userId = GetUserId();
        var result = await _projectService.GetProjectDocumentsAsync(projectId, userId);

        if (!result.Success || result.Data == null)
        {
            return NotFound(result);
        }

        return Ok(new
        {
            documents = result.Data
        });
    }

    [HttpPost("{projectId:guid}/documents")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadDocument([FromRoute] Guid projectId, [FromForm] UploadProjectDocumentRequestDto dto)
    {
        var userId = GetUserId();
        var result = await _projectService.UploadDocumentAsync(projectId, userId, dto);

        if (!result.Success || result.Data == null)
        {
            return BadRequest(result);
        }

        return Ok(new
        {
            documentId = result.Data.DocumentId,
            fileUrl = result.Data.FileUrl
        });
    }

    [HttpGet("{projectId:guid}/gallery")]
    public async Task<IActionResult> GetGallery([FromRoute] Guid projectId)
    {
        var userId = GetUserId();
        var result = await _projectService.GetProjectGalleryAsync(projectId, userId);

        if (!result.Success || result.Data == null)
        {
            return NotFound(result);
        }

        return Ok(new
        {
            images = result.Data
        });
    }

    [HttpPost("{projectId:guid}/gallery")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadGallery([FromRoute] Guid projectId, [FromForm] UploadProjectGalleryImageRequestDto dto)
    {
        var userId = GetUserId();
        var result = await _projectService.UploadGalleryImageAsync(projectId, userId, dto);

        if (!result.Success || result.Data == null)
        {
            return BadRequest(result);
        }

        return Ok(new
        {
            imageId = result.Data.ImageId,
            imageUrl = result.Data.ImageUrl
        });
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("User ID not found in token");
        }

        return userId;
    }
}
