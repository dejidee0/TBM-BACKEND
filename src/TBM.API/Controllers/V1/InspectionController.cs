using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TBM.Application.DTOs.Inspections;
using TBM.Application.Services;

namespace TBM.API.Controllers.V1;

[ApiController]
[Route("api/v1/inspections")]
[EnableRateLimiting("DynamicPolicy")]
public class InspectionController : ControllerBase
{
    private readonly InspectionService _inspectionService;

    public InspectionController(InspectionService inspectionService)
    {
        _inspectionService = inspectionService;
    }

    [HttpPost("verify-payment")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyPayment([FromBody] VerifyInspectionPaymentRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Reference))
        {
            return BadRequest(new { success = false, message = "Payment reference is required." });
        }

        var result = await _inspectionService.VerifyPaymentAsync(dto.Reference);
        return Ok(result);
    }

    [HttpPost("book")]
    [AllowAnonymous]
    public async Task<IActionResult> Book([FromBody] BookInspectionRequestDto dto)
    {
        try
        {
            var result = await _inspectionService.BookAsync(dto);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
}
