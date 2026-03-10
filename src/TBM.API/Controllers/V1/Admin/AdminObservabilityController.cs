using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TBM.API.Observability;

namespace TBM.API.Controllers.V1.Admin;

[ApiController]
[Route("api/admin/observability")]
[Authorize(Roles = "Admin,SuperAdmin")]
[EnableRateLimiting("DynamicPolicy")]
public class AdminObservabilityController : ControllerBase
{
    private readonly IEndpointMetricsStore _metricsStore;

    public AdminObservabilityController(IEndpointMetricsStore metricsStore)
    {
        _metricsStore = metricsStore;
    }

    [HttpGet("slo/overview")]
    public IActionResult GetSloOverview()
    {
        var snapshot = _metricsStore.GetSnapshot();
        var domains = snapshot.Domains
            .Where(x => x.Domain is "auth" or "checkout" or "ai")
            .ToList();

        return Ok(new
        {
            generatedAtUtc = snapshot.GeneratedAtUtc,
            domains
        });
    }

    [HttpGet("slo/{domain}")]
    public IActionResult GetSloByDomain(string domain)
    {
        var snapshot = _metricsStore.GetDomainSnapshot(domain);
        if (snapshot == null)
        {
            return NotFound(new
            {
                success = false,
                message = $"No observability data is available yet for '{domain}'."
            });
        }

        return Ok(snapshot);
    }
}
