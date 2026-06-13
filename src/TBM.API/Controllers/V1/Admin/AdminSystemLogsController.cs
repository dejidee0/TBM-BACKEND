using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TBM.Application.Services;

namespace TBM.API.Controllers.V1.Admin;

[ApiController]
[Route("api/v1/admin/system-logs")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminSystemLogsController : ControllerBase
{
    private readonly AdminSystemLogService _service;

    public AdminSystemLogsController(AdminSystemLogService service)
    {
        _service = service;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats([FromQuery] string? dateRange = null)
    {
        return Ok(await _service.GetStatsAsync(dateRange));
    }

    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? severity = null,
        [FromQuery] string? dateRange = null)
    {
        return Ok(await _service.GetLogsAsync(page, limit, search, severity, dateRange));
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] string? severity = null,
        [FromQuery] string? search = null,
        [FromQuery] string? dateRange = null)
    {
        var export = await _service.ExportAsync(severity, search, dateRange);
        return Ok(new
        {
            success = true,
            filename = export.FileName,
            contentType = export.ContentType,
            sizeBytes = export.Content.Length
        });
    }
}
