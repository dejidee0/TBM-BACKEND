using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TBM.Application.Interfaces;
using TBM.Application.Services;
using TBM.Core.Interfaces;

namespace TBM.API.Controllers.V1;

[ApiController]
[Route("api/v1/[controller]")]
[EnableRateLimiting("DynamicPolicy")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOrderService _orderService;
    private readonly UserDataStoreService _store;

    public DashboardController(IUnitOfWork unitOfWork, IOrderService orderService, UserDataStoreService store)
    {
        _unitOfWork = unitOfWork;
        _orderService = orderService;
        _store = store;
    }

    [HttpGet("~/api/dashboard/recent-order")]
    public async Task<IActionResult> RecentOrder()
    {
        var userId = GetUserId();
        var orders = await _unitOfWork.Orders.GetUserOrdersAsync(userId);
        var recent = orders.OrderByDescending(o => o.CreatedAt).FirstOrDefault();

        if (recent == null)
        {
            return Ok(new { hasOrder = false });
        }

        return Ok(new
        {
            hasOrder = true,
            orderId = recent.Id,
            orderNumber = recent.OrderNumber,
            status = recent.Status.ToString(),
            paymentStatus = recent.PaymentStatus.ToString(),
            total = recent.Total,
            createdAt = recent.CreatedAt
        });
    }

    [HttpGet("~/api/dashboard/latest-design")]
    public async Task<IActionResult> LatestDesign()
    {
        var userId = GetUserId();
        var projects = await _unitOfWork.AIProjects.GetByUserAsync(userId);
        var design = projects
            .SelectMany(p => p.Designs)
            .Where(d => !d.IsDeleted)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefault();

        if (design == null)
        {
            return Ok(new { hasDesign = false });
        }

        return Ok(new
        {
            hasDesign = true,
            designId = design.Id,
            outputUrl = design.OutputUrl,
            outputType = design.OutputType.ToString(),
            createdAt = design.CreatedAt
        });
    }

    [HttpGet("~/api/dashboard/consultations")]
    public IActionResult Consultations()
    {
        return Ok(new
        {
            upcomingCount = 0,
            completedCount = 0,
            nextConsultation = (object?)null
        });
    }

    [HttpGet("~/api/dashboard/saved-items")]
    public async Task<IActionResult> SavedPreview()
    {
        var userId = GetUserId();
        var state = await _store.GetAsync("UserSaved", UserDataStoreService.BuildUserKey("saved", userId), new SavedState());
        var latestIds = state.Items
            .OrderByDescending(i => i.SavedAt)
            .Take(4)
            .Select(i => i.Id)
            .ToList();

        return Ok(new
        {
            totalSaved = state.Items.Count,
            latestSavedIds = latestIds
        });
    }

    [HttpGet("~/api/dashboard/orders/{orderId:guid}/tracking")]
    public async Task<IActionResult> Tracking(Guid orderId)
    {
        var userId = GetUserId();
        var orderResult = await _orderService.GetOrderByIdAsync(orderId, userId);
        if (!orderResult.Success || orderResult.Data == null)
        {
            return NotFound(new { success = false, message = orderResult.Message });
        }

        var trackingUrl = string.IsNullOrWhiteSpace(orderResult.Data.TrackingNumber)
            ? null
            : $"https://tracking.example.com/track/{Uri.EscapeDataString(orderResult.Data.TrackingNumber)}";

        return Ok(new
        {
            success = true,
            trackingUrl
        });
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("User ID not found in token");
        }
        return userId;
    }

    private class SavedState
    {
        public List<SavedItemState> Items { get; set; } = new();
    }

    private class SavedItemState
    {
        public Guid Id { get; set; }
        public DateTime SavedAt { get; set; }
    }
}
