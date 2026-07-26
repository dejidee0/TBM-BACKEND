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

    [HttpPost("{id:guid}/initialize-payment")]
    [AllowAnonymous]
    public async Task<IActionResult> InitializePayment(Guid id, [FromBody] InitializeInspectionPaymentRequestDto dto)
    {
        try
        {
            var result = await _inspectionService.InitializePaymentAsync(id, dto.Email);
            if (result == null)
            {
                return NotFound(new { success = false, message = "Inspection booking not found." });
            }

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("verify-payment")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyPayment([FromBody] VerifyInspectionPaymentRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Reference))
        {
            return BadRequest(new { success = false, message = "Payment reference is required." });
        }

        try
        {
            var result = await _inspectionService.VerifyPaymentAsync(dto.Reference);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            // Amount mismatch between what Paystack reports and the server-side fee.
            return BadRequest(new { success = false, verified = false, message = ex.Message });
        }
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
