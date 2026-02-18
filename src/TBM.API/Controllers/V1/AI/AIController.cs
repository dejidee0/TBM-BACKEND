using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TBM.Application.Services;
using System.Security.Claims;
using TBM.Application.DTOs.AI;

namespace TBM.API.Controllers.V1.AI;

[ApiController]
[Route("api/v1/ai")]
[Authorize]
public class AIController : ControllerBase
{
    private readonly AIService _aiService;
    
    public AIController(AIService aiService)
    {
        _aiService = aiService;
    }
    
    [HttpPost("projects")]
    public async Task<IActionResult> CreateProject(CreateAIProjectDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
            return Unauthorized("User ID claim missing");
        
        var userId = Guid.Parse(userIdClaim.Value);
        var project = await _aiService.CreateProjectAsync(userId, dto);
        return Ok(project);
    }
    
    [HttpPost("generate/image")]
    public async Task<IActionResult> GenerateImage(GenerateImageDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
            return Unauthorized("User ID claim missing");
        
        var userId = Guid.Parse(userIdClaim.Value);
        var result = await _aiService.GenerateImageAsync(userId, dto);
        return Ok(result);
    }
    
    [HttpPost("transform/image")]
    public async Task<IActionResult> TransformImage([FromBody] GenerateImageDto dto)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var design = await _aiService.GenerateImageAsync(userId, dto);
            return Ok(design);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    } // ✅ FIXED: Added missing closing brace
    
    [HttpPost("generate/video")]
    public async Task<IActionResult> GenerateVideo([FromBody] GenerateVideoDto dto)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var design = await _aiService.GenerateVideoAsync(userId, dto);
            return Ok(design);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}