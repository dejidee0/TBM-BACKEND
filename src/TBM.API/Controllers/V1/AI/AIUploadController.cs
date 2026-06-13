using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TBM.Application.DTOs.AI;
using TBM.Application.Services;

namespace TBM.API.Controllers.V1.AI;

[ApiController]
[Route("api/v1/ai")]
[Authorize]
[EnableRateLimiting("DynamicPolicy")]
public class AIUploadController : ControllerBase
{
    private readonly ImageUploadService _uploadService;

    public AIUploadController(ImageUploadService uploadService)
    {
        _uploadService = uploadService;
    }

    [HttpPost("upload-room")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10_485_760)] // 10 MB
    public async Task<IActionResult> UploadRoom(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { success = false, message = "A source image file is required." });
        }

        if (file.Length > 10 * 1024 * 1024)
        {
            return BadRequest(new
            {
                success = false,
                message = $"Image file is too large ({file.Length / 1024 / 1024.0:F1} MB). Maximum allowed size is 10 MB.",
                maxSizeMb = 10
            });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized(new { success = false, message = "User ID claim missing." });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var imageUrl = await _uploadService.UploadRoomAsync(stream, file.FileName, userId);

            return Ok(new AIUploadRoomResponseDto { ImageUrl = imageUrl });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
}
