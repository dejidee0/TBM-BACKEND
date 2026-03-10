using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TBM.Application.Services;

namespace TBM.API.Controllers.V1.Admin;

[ApiController]
[Route("api/admin/financial")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminFinancialController : ControllerBase
{
    private readonly AdminFinancialService _service;

    public AdminFinancialController(AdminFinancialService service)
    {
        _service = service;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        return Ok(await _service.GetStatsAsync());
    }

    [HttpGet("monthly-revenue")]
    public async Task<IActionResult> GetMonthlyRevenue([FromQuery] int months = 12)
    {
        return Ok(await _service.GetMonthlyRevenueAsync(months));
    }

    [HttpGet("revenue-by-service")]
    public async Task<IActionResult> GetRevenueByService([FromQuery] string? dateRange = null)
    {
        return Ok(await _service.GetRevenueByServiceAsync(dateRange));
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? filter = null)
    {
        return Ok(await _service.GetTransactionsAsync(page, limit, search, filter));
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] string? search = null,
        [FromQuery] string? filter = null)
    {
        var export = await _service.ExportAsync(search, filter);
        return Ok(new
        {
            success = true,
            filename = export.FileName,
            contentType = export.ContentType,
            sizeBytes = export.Content.Length
        });
    }
}
