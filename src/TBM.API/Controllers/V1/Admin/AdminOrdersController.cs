using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TBM.Application.DTOs.Admin;
using TBM.Application.Services;
using TBM.Core.Enums;

[ApiController]
[Route("api/admin/orders")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminOrdersController : ControllerBase
{
    private readonly AdminOrderService _service;

    public AdminOrdersController(AdminOrderService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetOrders(
        int page = 1,
        int pageSize = 20,
        OrderStatus? status = null,
        string? search = null)
    {
        var result = await _service.GetOrdersAsync(page, pageSize, status, search);
        return Ok(result);
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] OrderStatus newStatus)
    {
        var adminId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        await _service.UpdateStatusAsync(id, newStatus, adminId, null);
        return Ok("Status updated");
    }

    [HttpPatch("{id}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] string reason)
    {
        var adminId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        await _service.CancelOrderAsync(id, adminId, reason);
        return Ok("Order cancelled");
    }

    [HttpPost("{id}/refund")]
    public async Task<IActionResult> Refund(Guid id, [FromBody] string reason)
    {
        var adminId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        await _service.RefundOrderAsync(id, adminId, reason);
        return Ok("Order refunded");
    }

    [HttpPatch("{id}/tracking")]
    public async Task<IActionResult> UpdateTracking(Guid id, TrackingDto dto)
    {
        await _service.UpdateTrackingAsync(id, dto.TrackingNumber);
        return Ok("Tracking updated");
    }
}
