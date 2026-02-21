using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TBM.Application.DTOs.Vendor;
using TBM.Application.Services;
using TBM.Core.Enums;

namespace TBM.API.Controllers.V1;

[ApiController]
[Route("api/vendor")]
[Route("api/v1/vendor")]
[Authorize(Roles = "Vendor")]
public class VendorController : ControllerBase
{
    private readonly VendorDomainService _vendorService;

    public VendorController(VendorDomainService vendorService)
    {
        _vendorService = vendorService;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var vendorId = GetVendorId();
        var result = await _vendorService.GetDashboardAsync(vendorId);
        return Ok(result);
    }

    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts()
    {
        var vendorId = GetVendorId();
        var result = await _vendorService.GetAlertsAsync(vendorId);
        return Ok(result);
    }

    [HttpGet("activity")]
    public async Task<IActionResult> GetActivity([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var vendorId = GetVendorId();
        var result = await _vendorService.GetActivityAsync(vendorId, NormalizePage(page), NormalizePageSize(pageSize));
        return Ok(result);
    }

    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] OrderStatus? status = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] bool assignedOnly = false)
    {
        var vendorId = GetVendorId();
        var result = await _vendorService.GetOrdersAsync(
            vendorId,
            NormalizePage(page),
            NormalizePageSize(pageSize),
            status,
            search,
            fromDate,
            toDate,
            assignedOnly);

        return Ok(result);
    }

    [HttpGet("orders/{orderId:guid}")]
    public async Task<IActionResult> GetOrder(Guid orderId)
    {
        try
        {
            var vendorId = GetVendorId();
            var result = await _vendorService.GetOrderDetailAsync(vendorId, orderId);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPatch("orders/{orderId:guid}/status")]
    public async Task<IActionResult> UpdateOrderStatus(Guid orderId, [FromBody] VendorOrderStatusUpdateRequest request)
    {
        try
        {
            var vendorId = GetVendorId();
            await _vendorService.UpdateOrderStatusAsync(vendorId, orderId, request.Status, request.Note);
            return Ok(new { success = true });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("orders/{orderId:guid}/notes")]
    public async Task<IActionResult> AddOrderNote(Guid orderId, [FromBody] VendorOrderNoteRequest request)
    {
        try
        {
            var vendorId = GetVendorId();
            var result = await _vendorService.AddOrderNoteAsync(vendorId, orderId, request.Note);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPatch("orders/{orderId:guid}/assignment")]
    public async Task<IActionResult> UpdateDeliveryAssignment(Guid orderId, [FromBody] VendorOrderAssignmentRequest request)
    {
        try
        {
            var vendorId = GetVendorId();
            var result = await _vendorService.AssignDeliveryAsync(vendorId, orderId, request);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("inventory")]
    public async Task<IActionResult> GetInventory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool lowStockOnly = false)
    {
        var vendorId = GetVendorId();
        var result = await _vendorService.GetInventoryAsync(
            vendorId,
            NormalizePage(page),
            NormalizePageSize(pageSize),
            search,
            lowStockOnly);

        return Ok(result);
    }

    [HttpPut("inventory/{productId:guid}")]
    public async Task<IActionResult> UpdateInventory(Guid productId, [FromBody] VendorInventoryUpdateRequest request)
    {
        try
        {
            var vendorId = GetVendorId();
            var result = await _vendorService.UpdateInventoryAsync(vendorId, productId, request);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("deliveries")]
    public async Task<IActionResult> GetDeliveries(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] OrderStatus? status = null)
    {
        var vendorId = GetVendorId();
        var result = await _vendorService.GetDeliveriesAsync(
            vendorId,
            NormalizePage(page),
            NormalizePageSize(pageSize),
            status);

        return Ok(result);
    }

    [HttpGet("messages")]
    public async Task<IActionResult> GetMessages(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool unreadOnly = false)
    {
        var vendorId = GetVendorId();
        var result = await _vendorService.GetMessagesAsync(vendorId, NormalizePage(page), NormalizePageSize(pageSize), unreadOnly);
        return Ok(result);
    }

    [HttpPost("messages")]
    public async Task<IActionResult> SendMessage([FromBody] VendorMessageCreateRequest request)
    {
        try
        {
            var vendorId = GetVendorId();
            var result = await _vendorService.SendMessageAsync(vendorId, request);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool unreadOnly = false)
    {
        var vendorId = GetVendorId();
        var result = await _vendorService.GetNotificationsPagedAsync(vendorId, NormalizePage(page), NormalizePageSize(pageSize), unreadOnly);
        return Ok(result);
    }

    [HttpPatch("notifications/{notificationId}/read")]
    public async Task<IActionResult> MarkNotificationRead(string notificationId)
    {
        try
        {
            var vendorId = GetVendorId();
            await _vendorService.MarkNotificationReadAsync(vendorId, notificationId);
            return Ok(new { success = true });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    private Guid GetVendorId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("User ID not found in token.");
        }

        return userId;
    }

    private static int NormalizePage(int page) => page < 1 ? 1 : page;
    private static int NormalizePageSize(int pageSize) => pageSize < 1 ? 20 : Math.Min(pageSize, 100);
}
