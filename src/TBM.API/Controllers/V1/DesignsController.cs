using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TBM.Application.Services;
using TBM.Core.Enums;
using TBM.Core.Interfaces;

namespace TBM.API.Controllers.V1;

[ApiController]
[Route("api/v1/[controller]")]
[EnableRateLimiting("DynamicPolicy")]
[Authorize]
public class DesignsController : ControllerBase
{
    private const string Category = "UserDesigns";
    private const string KeyRoot = "designprefs";

    private readonly IUnitOfWork _unitOfWork;
    private readonly UserDataStoreService _store;
    private readonly AuditService _auditService;

    public DesignsController(IUnitOfWork unitOfWork, UserDataStoreService store, AuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _store = store;
        _auditService = auditService;
    }

    [HttpGet("~/api/designs")]
    public async Task<IActionResult> GetDesigns(
        [FromQuery] string? roomType = null,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10)
    {
        var userId = GetUserId();
        var projects = await _unitOfWork.AIProjects.GetByUserAsync(userId);
        var favorites = await GetFavoritesAsync(userId);

        var rows = projects
            .SelectMany(project => project.Designs.Select(design => new
            {
                Design = design,
                Project = project
            }))
            .Where(x => !x.Design.IsDeleted)
            .Select(x => new DesignRow
            {
                Id = x.Design.Id,
                ProjectId = x.Project.Id,
                RoomType = x.Project.ContextLabel,
                Prompt = x.Project.Prompt,
                Url = x.Design.OutputUrl,
                OutputType = x.Design.OutputType.ToString(),
                CreatedAt = x.Design.CreatedAt,
                IsFavorite = favorites.Contains(x.Design.Id)
            })
            .ToList();

        if (!string.IsNullOrWhiteSpace(roomType))
        {
            rows = rows
                .Where(x => string.Equals(x.RoomType, roomType, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            rows = rows
                .Where(x =>
                    (!string.IsNullOrWhiteSpace(x.Prompt) && x.Prompt.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(x.RoomType) && x.RoomType.Contains(search, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        rows = sortBy?.Trim().ToLowerInvariant() switch
        {
            "oldest" => rows.OrderBy(x => x.CreatedAt).ToList(),
            _ => rows.OrderByDescending(x => x.CreatedAt).ToList()
        };

        var safePage = page < 1 ? 1 : page;
        var safeLimit = limit < 1 ? 10 : limit;
        var total = rows.Count;
        var paged = rows.Skip((safePage - 1) * safeLimit).Take(safeLimit).ToList();
        var totalPages = (int)Math.Ceiling(total / (double)safeLimit);

        return Ok(new
        {
            designs = paged,
            pagination = new
            {
                page = safePage,
                limit = safeLimit,
                total,
                totalPages,
                hasMore = safePage < totalPages
            }
        });
    }

    [HttpGet("~/api/designs/{id:guid}")]
    public async Task<IActionResult> GetDesignById(Guid id)
    {
        var userId = GetUserId();
        var design = await _unitOfWork.AIDesigns.GetByIdAsync(id);
        if (design == null || design.IsDeleted || design.AIProject.UserId != userId)
        {
            return NotFound(new { success = false, message = "Design not found" });
        }

        var favorites = await GetFavoritesAsync(userId);

        return Ok(new
        {
            id = design.Id,
            projectId = design.AIProjectId,
            outputUrl = design.OutputUrl,
            outputType = design.OutputType.ToString(),
            width = design.Width,
            height = design.Height,
            durationSeconds = design.DurationSeconds,
            prompt = design.AIProject.Prompt,
            roomType = design.AIProject.ContextLabel,
            isFavorite = favorites.Contains(design.Id),
            createdAt = design.CreatedAt
        });
    }

    [HttpPost("~/api/designs/{id:guid}/favorite")]
    public async Task<IActionResult> ToggleFavorite(Guid id)
    {
        var userId = GetUserId();
        var design = await _unitOfWork.AIDesigns.GetByIdAsync(id);
        if (design == null || design.IsDeleted || design.AIProject.UserId != userId)
        {
            return NotFound(new { success = false, message = "Design not found" });
        }

        var favorites = await GetFavoritesAsync(userId);
        var isFavorite = favorites.Contains(id);
        if (isFavorite)
        {
            favorites.Remove(id);
            isFavorite = false;
        }
        else
        {
            favorites.Add(id);
            isFavorite = true;
        }

        await SaveFavoritesAsync(userId, favorites);
        await _auditService.LogAsync("Design.Favorite.Toggle", "DesignLibrary", null, new { userId, designId = id, isFavorite });

        return Ok(new { success = true, isFavorite });
    }

    [HttpDelete("~/api/designs/{id:guid}")]
    public async Task<IActionResult> DeleteDesign(Guid id)
    {
        var userId = GetUserId();
        var design = await _unitOfWork.AIDesigns.GetByIdAsync(id);
        if (design == null || design.IsDeleted || design.AIProject.UserId != userId)
        {
            return NotFound(new { success = false, message = "Design not found" });
        }

        design.IsDeleted = true;
        design.DeletedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync("Design.Delete", "DesignLibrary", new { designId = id }, null);
        return Ok(new { success = true });
    }

    [HttpGet("~/api/designs/{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, [FromQuery] string quality = "standard")
    {
        var userId = GetUserId();
        var design = await _unitOfWork.AIDesigns.GetByIdAsync(id);
        if (design == null || design.IsDeleted || design.AIProject.UserId != userId)
        {
            return NotFound(new { success = false, message = "Design not found" });
        }

        var downloadUrl = quality.Trim().ToLowerInvariant() == "high"
            ? $"{design.OutputUrl}?quality=high"
            : $"{design.OutputUrl}?quality=standard";

        return Ok(new { success = true, downloadUrl });
    }

    [HttpPost("~/api/designs/{id:guid}/share")]
    public async Task<IActionResult> Share(Guid id)
    {
        var userId = GetUserId();
        var design = await _unitOfWork.AIDesigns.GetByIdAsync(id);
        if (design == null || design.IsDeleted || design.AIProject.UserId != userId)
        {
            return NotFound(new { success = false, message = "Design not found" });
        }

        var shareToken = Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant();
        var shareUrl = $"{Request.Scheme}://{Request.Host}/api/designs/{id}/download?token={shareToken}";

        await _auditService.LogAsync("Design.Share", "DesignLibrary", null, new { userId, designId = id, shareToken });
        return Ok(new { success = true, shareUrl });
    }

    private async Task<List<Guid>> GetFavoritesAsync(Guid userId)
    {
        var key = UserDataStoreService.BuildUserKey(KeyRoot, userId);
        var state = await _store.GetAsync(Category, key, new DesignPreferenceState());
        return state.FavoriteDesignIds;
    }

    private async Task SaveFavoritesAsync(Guid userId, List<Guid> favorites)
    {
        var key = UserDataStoreService.BuildUserKey(KeyRoot, userId);
        await _store.SaveAsync(Category, key, new DesignPreferenceState
        {
            FavoriteDesignIds = favorites.Distinct().ToList()
        }, "Design library user preferences");
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

    private class DesignPreferenceState
    {
        public List<Guid> FavoriteDesignIds { get; set; } = new();
    }

    private class DesignRow
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string? RoomType { get; set; }
        public string? Prompt { get; set; }
        public string Url { get; set; } = string.Empty;
        public string OutputType { get; set; } = AIOutputType.Image.ToString();
        public DateTime CreatedAt { get; set; }
        public bool IsFavorite { get; set; }
    }
}
