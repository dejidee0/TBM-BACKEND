using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TBM.Application.Services;

namespace TBM.API.Controllers.V1.AI
{
    [ApiController]
    [Route("api/v1/ai")]
    [Authorize]
    public class AIUploadController : ControllerBase
    {
        private readonly ImageUploadService _uploadService;

        public AIUploadController(ImageUploadService uploadService)
        {
            _uploadService = uploadService;
        }

      [HttpPost("upload-room")]
[Consumes("multipart/form-data")]
public async Task<IActionResult> UploadRoom(IFormFile file)

{
    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId == null) return Unauthorized();

    using var stream = file.OpenReadStream();
    var imageUrl = await _uploadService.UploadRoomAsync(stream, file.FileName, userId);

    return Ok(new { success = true, imageUrl });
}

    }
}
