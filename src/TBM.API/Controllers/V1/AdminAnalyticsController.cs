using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TBM.Application.Services;

namespace TBM.API.Controllers.V1.Admin;

[ApiController]
[Route("api/admin/analytics")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminAnalyticsController : ControllerBase
{
    private readonly AdminAnalyticsService _service;

    public AdminAnalyticsController(AdminAnalyticsService service)
    {
        _service = service;
    }

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview()
        => Ok(await _service.GetOverviewAsync());

    [HttpGet("monthly-revenue")]
    public async Task<IActionResult> GetMonthlyRevenue()
        => Ok(await _service.GetMonthlyRevenueAsync());

    [HttpGet("payment-distribution")]
    public async Task<IActionResult> GetPaymentDistribution()
        => Ok(await _service.GetPaymentDistributionAsync());
}