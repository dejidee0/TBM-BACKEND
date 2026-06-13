using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TBM.Application.DTOs.DesignFlow;
using TBM.Application.Services.DesignFlow;

namespace TBM.API.Controllers.V1;

[ApiController]
[Route("api/v1/designs/sessions")]
[EnableRateLimiting("DynamicPolicy")]
[Authorize]
public class DesignSessionsController : ControllerBase
{
    private readonly DesignSessionService _designSessionService;

    public DesignSessionsController(DesignSessionService designSessionService)
    {
        _designSessionService = designSessionService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateSession([FromBody] CreateDesignSessionRequestDto dto)
    {
        var userId = GetUserId();
        var result = await _designSessionService.CreateSessionAsync(userId, dto);

        if (!result.Success || result.Data == null)
        {
            return BadRequest(result);
        }

        return Ok(new
        {
            sessionId = result.Data.SessionId,
            sessionNumber = result.Data.SessionNumber,
            status = result.Data.Status,
            uploadUrl = result.Data.UploadUrl
        });
    }

    [HttpPost("{sessionId:guid}/upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadPhoto([FromRoute] Guid sessionId, [FromForm] UploadDesignSessionPhotoRequestDto dto)
    {
        var userId = GetUserId();
        var result = await _designSessionService.UploadPhotoAsync(userId, sessionId, dto.Image);

        if (!result.Success || result.Data == null)
        {
            return BadRequest(result);
        }

        return Ok(new
        {
            originalImageUrl = result.Data.OriginalImageUrl,
            status = result.Data.Status
        });
    }

    [HttpPost("{sessionId:guid}/generate")]
    public async Task<IActionResult> Generate([FromRoute] Guid sessionId, [FromBody] GenerateDesignSessionRequestDto dto)
    {
        if (!dto.GenerateBOM)
        {
            return BadRequest(new
            {
                success = false,
                message = "generateBOM must be true for the current flow."
            });
        }

        var userId = GetUserId();
        var result = await _designSessionService.RequestGenerationAsync(userId, sessionId);

        if (!result.Success || result.Data == null)
        {
            return BadRequest(result);
        }

        return Ok(new
        {
            status = result.Data.Status,
            estimatedTime = result.Data.EstimatedTime,
            statusUrl = result.Data.StatusUrl
        });
    }

    [HttpGet("{sessionId:guid}/status")]
    public async Task<IActionResult> GetStatus([FromRoute] Guid sessionId)
    {
        var userId = GetUserId();
        var result = await _designSessionService.GetSessionStatusAsync(userId, sessionId);

        if (!result.Success || result.Data == null)
        {
            return NotFound(result);
        }

        return Ok(new
        {
            status = result.Data.Status,
            progress = result.Data.Progress,
            currentStep = result.Data.CurrentStep,
            imageUrl = result.Data.ImageUrl,
            bomGenerated = result.Data.BomGenerated,
            errorMessage = result.Data.ErrorMessage
        });
    }

    [HttpGet("{sessionId:guid}")]
    public async Task<IActionResult> GetSession([FromRoute] Guid sessionId)
    {
        var userId = GetUserId();
        var result = await _designSessionService.GetSessionAsync(userId, sessionId);

        if (!result.Success || result.Data == null)
        {
            return NotFound(result);
        }

        return Ok(new
        {
            session = result.Data.Session,
            billOfMaterials = result.Data.BillOfMaterials,
            allItemsInStock = result.Data.AllItemsInStock
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetSessions()
    {
        var userId = GetUserId();
        var result = await _designSessionService.GetSessionsAsync(userId);

        if (!result.Success || result.Data == null)
        {
            return BadRequest(result);
        }

        return Ok(new
        {
            sessions = result.Data.Sessions
        });
    }

    [HttpPost("{sessionId:guid}/add-to-cart")]
    public async Task<IActionResult> AddToCart([FromRoute] Guid sessionId, [FromBody] AddDesignSessionToCartRequestDto dto)
    {
        var userId = GetUserId();
        var result = await _designSessionService.AddToCartAsync(userId, sessionId, dto);

        if (!result.Success || result.Data == null)
        {
            return BadRequest(result);
        }

        return Ok(new
        {
            cartId = result.Data.CartId,
            itemsAdded = result.Data.ItemsAdded,
            totalAmount = result.Data.TotalAmount
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
}
