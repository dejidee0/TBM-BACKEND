
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

    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications()
    {
        var data = await _service.GetNotificationsAsync();
        return Ok(data);
    }

    [HttpPut("notifications")]
    public async Task<IActionResult> UpdateNotifications(AdminNotificationSettingsDto dto)
    {
        await _service.SaveNotificationsAsync(dto);
        return Ok(new { success = true, message = "Notification settings updated" });
    }

    [HttpPut]
    public async Task<IActionResult> UpdateSettingsForm([FromBody] LegacyAdminSettingsFormDto dto)
    {
        await _service.SaveLegacyPaymentFormAsync(
            dto.BaseFee,
            dto.FixedFee,
            dto.Currency);

        return Ok(new { success = true, message = "Settings updated" });
    }

    [HttpPatch("payment/gateways/{gatewayId}")]
    public async Task<IActionResult> PatchPaymentGateway(
        string gatewayId,
        [FromBody] ToggleEnabledDto dto)
    {
        var gateway = await _service.PatchPaymentGatewayAsync(gatewayId, dto.Enabled);
        return Ok(new { success = true, gateway });
    }

    [HttpPatch("ai/models/{modelId}")]
    public async Task<IActionResult> PatchAIModel(
        string modelId,
        [FromBody] ToggleEnabledDto dto)
    {
        var model = await _service.PatchAIModelAsync(modelId, dto.Enabled);
        return Ok(new { success = true, model });
    }
}

public class LegacyAdminSettingsFormDto
{
    public decimal BaseFee { get; set; }
    public decimal FixedFee { get; set; }
    public string Currency { get; set; } = "USD";
}
