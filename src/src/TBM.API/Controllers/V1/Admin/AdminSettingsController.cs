
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TBM.Application.DTOs.Settings;
using TBM.Application.Services;


[ApiController]
[Route("api/admin/settings")]
[Authorize(Roles = "Admin,SuperAdmin")]
[EnableRateLimiting("DynamicPolicy")]

public class AdminSettingsController : ControllerBase
{
    private readonly AdminSettingsService _service;

    public AdminSettingsController(AdminSettingsService service)
    {
        _service = service;
    }

    [HttpGet("payment")]
    public async Task<IActionResult> GetPayment()
    {
        var data = await _service.GetCategoryAsync<PaymentSettingsDto>("Payment")
            ?? new PaymentSettingsDto();
        return Ok(data);
    }

    [HttpPut("payment")]
    public async Task<IActionResult> UpdatePayment(PaymentSettingsDto dto)
    {
        await _service.SaveCategoryAsync("Payment", dto);
        return Ok("Payment settings updated");
    }

    [HttpGet("ai")]
    public async Task<IActionResult> GetAI()
    {
        var data = await _service.GetCategoryAsync<AISettingsDto>("AI")
            ?? new AISettingsDto();
        return Ok(data);
    }

    [HttpPut("ai")]
    public async Task<IActionResult> UpdateAI(AISettingsDto dto)
    {
        await _service.SaveCategoryAsync("AI", dto);
        return Ok("AI settings updated");
    }

    [HttpGet("general")]
    public async Task<IActionResult> GetGeneral()
    {
        var data = await _service.GetCategoryAsync<GeneralSettingsDto>("General")
            ?? new GeneralSettingsDto
            {
                PlatformName = string.Empty,
                SupportEmail = string.Empty
            };
        return Ok(data);
    }

    [HttpPut("general")]
    public async Task<IActionResult> UpdateGeneral(GeneralSettingsDto dto)
    {
        await _service.SaveCategoryAsync("General", dto);
        return Ok("General settings updated");
    }
}
