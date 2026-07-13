using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TBM.Application.DTOs.ProjectRequests;
using TBM.Application.Services;

namespace TBM.API.Controllers.V1;

[ApiController]
[Route("api/v1/project-requests")]
[EnableRateLimiting("DynamicPolicy")]
public class ProjectRequestsController : ControllerBase
{
    private readonly ProjectRequestService _projectRequestService;

    public ProjectRequestsController(ProjectRequestService projectRequestService)
    {
        _projectRequestService = projectRequestService;
    }

    [HttpPost("3d-model")]
    [AllowAnonymous]
    public Task<IActionResult> Request3DModel([FromBody] CreateProjectRequestDto dto)
        => CreateAsync("3DModel", dto);

    [HttpPost("boq")]
    [AllowAnonymous]
    public Task<IActionResult> RequestBoq([FromBody] CreateProjectRequestDto dto)
        => CreateAsync("BOQ", dto);

    [HttpPost("contact-designer")]
    [AllowAnonymous]
    public Task<IActionResult> RequestDesignerContact([FromBody] CreateProjectRequestDto dto)
        => CreateAsync("DesignerContact", dto);

    private async Task<IActionResult> CreateAsync(string requestType, CreateProjectRequestDto dto)
    {
        try
        {
            var result = await _projectRequestService.CreateAsync(requestType, dto);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
}
