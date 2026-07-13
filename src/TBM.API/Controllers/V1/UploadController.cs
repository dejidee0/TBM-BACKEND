using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TBM.Application.Services;

namespace TBM.API.Controllers.V1;

[ApiController]
[Route("api/v1/uploads")]
[EnableRateLimiting("DynamicPolicy")]
public class UploadController : ControllerBase
{
    private readonly DocumentUploadService _documentUploadService;

    public UploadController(DocumentUploadService documentUploadService)
    {
        _documentUploadService = documentUploadService;
    }

    [HttpPost("document")]
    [AllowAnonymous]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(52_428_800)] // 50 MB
    public async Task<IActionResult> UploadDocument(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { success = false, message = "A file is required." });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var url = await _documentUploadService.UploadDocumentAsync(stream, file.FileName, file.ContentType);
            return Ok(new { url });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
}
