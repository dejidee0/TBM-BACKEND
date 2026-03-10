using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TBM.Application.Services;

namespace TBM.API.Controllers.V1.Admin;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminDashboardController : ControllerBase
{
    private readonly AdminDashboardService _service;

    public AdminDashboardController(AdminDashboardService service)
    {
        _service = service;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        return Ok(await _service.GetStatsAsync());
    }

    [HttpGet("revenue")]
    public async Task<IActionResult> GetRevenue([FromQuery] string? timeRange = "30d")
    {
        return Ok(await _service.GetRevenueAsync(timeRange));
    }

    [HttpGet("server-load")]
    public async Task<IActionResult> GetServerLoad()
    {
        return Ok(await _service.GetServerLoadAsync());
    }

    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts()
    {
        return Ok(await _service.GetAlertsAsync());
    }

    [HttpGet("quick-actions")]
    public async Task<IActionResult> GetQuickActions()
    {
        return Ok(await _service.GetQuickActionsAsync());
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        return Ok(await _service.RefreshAsync());
    }

    [HttpPost("export")]
    public async Task<IActionResult> Export()
    {
        var export = await _service.ExportAsync();
        return Ok(new
        {
            success = true,
            filename = export.FileName,
            contentType = export.ContentType,
            sizeBytes = export.Content.Length
        });
    }
}
