using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TBM.Application.DTOs.AI;
using TBM.Core.Interfaces;

namespace TBM.API.Controllers.V1;

[ApiController]
[Route("api/v1/public/projects")]
[EnableRateLimiting("DynamicPolicy")]
public class PublicProjectsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public PublicProjectsController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetPublicProjects(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 12,
        [FromQuery] string? roomType = null,
        [FromQuery] string? search = null,
        [FromQuery] string? sort = null)
    {
        var (items, total) = await _unitOfWork.AIDesigns.GetPublicPagedAsync(
            page,
            limit,
            roomType,
            search,
            sort);

        var safePage = page < 1 ? 1 : page;
        var safeLimit = limit < 1 ? 12 : Math.Min(limit, 50);
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)safeLimit);

        var projects = items.Select(design => new PublicProjectListItemDto
        {
            DesignId = design.Id,
            ProjectId = design.AIProjectId,
            OutputUrl = design.OutputUrl,
            OutputType = design.OutputType,
            RoomType = design.AIProject.ContextLabel,
            Prompt = design.AIProject.Prompt,
            CreatedAt = design.CreatedAt,
            PublishedAt = design.PublishedAt
        });

        return Ok(new
        {
            projects,
            pagination = new
            {
                page = safePage,
                limit = safeLimit,
                total,
                totalPages,
                hasMore = totalPages > 0 && safePage < totalPages
            }
        });
    }
}
