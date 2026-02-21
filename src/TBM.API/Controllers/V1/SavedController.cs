using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TBM.Application.DTOs.Orders;
using TBM.Application.Interfaces;
using TBM.Application.Services;
using TBM.Core.Interfaces;

namespace TBM.API.Controllers.V1;

[ApiController]
[Route("api/v1/[controller]")]
[EnableRateLimiting("DynamicPolicy")]
[Authorize]
public class SavedController : ControllerBase
{
    private const string SavedCategory = "UserSaved";
    private const string SavedRootKey = "saved";

    private readonly UserDataStoreService _store;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICartService _cartService;
    private readonly AuditService _auditService;

    public SavedController(
        UserDataStoreService store,
        IUnitOfWork unitOfWork,
        ICartService cartService,
        AuditService auditService)
    {
        _store = store;
        _unitOfWork = unitOfWork;
        _cartService = cartService;
        _auditService = auditService;
    }

    [HttpGet("~/api/saved")]
    public async Task<IActionResult> GetSaved(
        [FromQuery] string? category = null,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10)
    {
        var userId = GetUserId();
        var state = await GetStateAsync(userId);

        var rows = new List<SavedRow>();
        foreach (var item in state.Items)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId);
            if (product == null || !product.IsActive)
            {
                continue;
            }

            rows.Add(new SavedRow
            {
                Id = item.Id,
                ProductId = product.Id,
                Name = product.Name,
                Category = product.Category?.Name,
                Price = product.Price ?? 0m,
                Image = product.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                    ?? product.Images.FirstOrDefault()?.ImageUrl,
                SavedAt = item.SavedAt
            });
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            rows = rows
                .Where(x => string.Equals(x.Category, category, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            rows = rows
                .Where(x => x.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        rows = sortBy?.Trim().ToLowerInvariant() switch
        {
            "price_asc" => rows.OrderBy(x => x.Price).ToList(),
            "price_desc" => rows.OrderByDescending(x => x.Price).ToList(),
            "oldest" => rows.OrderBy(x => x.SavedAt).ToList(),
            _ => rows.OrderByDescending(x => x.SavedAt).ToList()
        };

        var safePage = page < 1 ? 1 : page;
        var safeLimit = limit < 1 ? 10 : limit;
        var total = rows.Count;
        var paged = rows
            .Skip((safePage - 1) * safeLimit)
            .Take(safeLimit)
            .ToList();
        var totalPages = (int)Math.Ceiling(total / (double)safeLimit);

        return Ok(new
        {
            items = paged,
            boards = state.Boards.Select(b => new
            {
                id = b.Id,
                name = b.Name,
                itemCount = b.ItemIds.Count,
                createdAt = b.CreatedAt
            }),
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

    [HttpPost("~/api/saved")]
    public async Task<IActionResult> SaveItem([FromBody] SaveItemRequest request)
    {
        var userId = GetUserId();
        var product = await _unitOfWork.Products.GetByIdAsync(request.ItemId);
        if (product == null || !product.IsActive)
        {
            return NotFound(new { success = false, message = "Product not found" });
        }

        var state = await GetStateAsync(userId);
        var exists = state.Items.Any(i => i.ProductId == request.ItemId);
        if (!exists)
        {
            state.Items.Add(new SavedItemState
            {
                Id = Guid.NewGuid(),
                ProductId = request.ItemId,
                SavedAt = DateTime.UtcNow
            });
            await SaveStateAsync(userId, state);

            await _auditService.LogAsync(
                "Saved.Add",
                "UserSaved",
                null,
                new { userId, productId = request.ItemId });
        }

        return Ok(new { success = true });
    }

    [HttpDelete("~/api/saved/{id:guid}")]
    public async Task<IActionResult> DeleteSaved(Guid id)
    {
        var userId = GetUserId();
        var state = await GetStateAsync(userId);
        var removed = state.Items.RemoveAll(x => x.Id == id);

        if (removed == 0)
        {
            return NotFound(new { success = false, message = "Saved item not found" });
        }

        foreach (var board in state.Boards)
        {
            board.ItemIds.RemoveAll(itemId => itemId == id);
        }

        await SaveStateAsync(userId, state);
        await _auditService.LogAsync("Saved.Delete", "UserSaved", new { savedId = id }, null);

        return Ok(new { success = true });
    }

    [HttpPost("~/api/saved/{id:guid}/add-to-cart")]
    public async Task<IActionResult> AddSavedToCart(Guid id, [FromBody] AddSavedToCartRequest request)
    {
        var userId = GetUserId();
        var state = await GetStateAsync(userId);
        var saved = state.Items.FirstOrDefault(x => x.Id == id);

        if (saved == null)
        {
            return NotFound(new { success = false, message = "Saved item not found" });
        }

        var addResult = await _cartService.AddToCartAsync(userId, new AddToCartDto
        {
            ProductId = saved.ProductId,
            Quantity = request.Quantity <= 0 ? 1 : request.Quantity
        });

        if (!addResult.Success)
        {
            return BadRequest(new { success = false, message = addResult.Message });
        }

        return Ok(new { success = true, message = "Item added to cart" });
    }

    [HttpPost("~/api/saved/{id:guid}/add-to-moodboard")]
    public async Task<IActionResult> AddToMoodboard(Guid id, [FromBody] AddToMoodboardRequest request)
    {
        var userId = GetUserId();
        var state = await GetStateAsync(userId);
        var saved = state.Items.FirstOrDefault(x => x.Id == id);

        if (saved == null)
        {
            return NotFound(new { success = false, message = "Saved item not found" });
        }

        SavedBoardState board;
        if (request.BoardId.HasValue)
        {
            board = state.Boards.FirstOrDefault(b => b.Id == request.BoardId.Value)
                ?? new SavedBoardState
                {
                    Id = request.BoardId.Value,
                    Name = "My Moodboard",
                    CreatedAt = DateTime.UtcNow
                };
            if (!state.Boards.Any(b => b.Id == board.Id))
            {
                state.Boards.Add(board);
            }
        }
        else
        {
            board = state.Boards.FirstOrDefault()
                ?? new SavedBoardState
                {
                    Id = Guid.NewGuid(),
                    Name = "My Moodboard",
                    CreatedAt = DateTime.UtcNow
                };
            if (!state.Boards.Any(b => b.Id == board.Id))
            {
                state.Boards.Add(board);
            }
        }

        if (!board.ItemIds.Contains(saved.Id))
        {
            board.ItemIds.Add(saved.Id);
        }

        await SaveStateAsync(userId, state);
        await _auditService.LogAsync("Moodboard.AddItem", "UserSaved", null, new { userId, boardId = board.Id, savedId = id });

        return Ok(new { success = true, message = "Item added to moodboard" });
    }

    [HttpPost("~/api/saved/create-board")]
    public async Task<IActionResult> CreateBoard([FromBody] CreateBoardRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.BoardName))
        {
            return BadRequest(new { success = false, message = "Board name is required" });
        }

        var userId = GetUserId();
        var state = await GetStateAsync(userId);

        var board = new SavedBoardState
        {
            Id = Guid.NewGuid(),
            Name = request.BoardName.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        foreach (var rawId in request.ItemIds.Distinct())
        {
            var saved = state.Items.FirstOrDefault(i => i.Id == rawId);
            if (saved != null)
            {
                board.ItemIds.Add(saved.Id);
            }
        }

        state.Boards.Add(board);
        await SaveStateAsync(userId, state);

        await _auditService.LogAsync("Moodboard.Create", "UserSaved", null, new
        {
            userId,
            boardId = board.Id,
            board.Name,
            itemCount = board.ItemIds.Count
        });

        return Ok(new
        {
            success = true,
            boardId = board.Id,
            boardName = board.Name,
            itemCount = board.ItemIds.Count
        });
    }

    [HttpPost("~/api/saved/buy-all")]
    public async Task<IActionResult> BuyAll([FromBody] BuyAllRequest request)
    {
        var userId = GetUserId();
        var state = await GetStateAsync(userId);

        var savedItems = state.Items
            .Where(i => request.ItemIds.Contains(i.Id))
            .ToList();

        if (savedItems.Count == 0)
        {
            return BadRequest(new { success = false, message = "No valid saved items provided" });
        }

        decimal total = 0m;
        foreach (var item in savedItems)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId);
            if (product == null || !product.IsActive)
            {
                continue;
            }

            total += product.Price ?? 0m;
            await _cartService.AddToCartAsync(userId, new AddToCartDto
            {
                ProductId = item.ProductId,
                Quantity = 1
            });
        }

        await _auditService.LogAsync("Saved.BuyAll", "UserSaved", null, new
        {
            userId,
            itemCount = savedItems.Count,
            total
        });

        return Ok(new
        {
            success = true,
            total,
            itemCount = savedItems.Count
        });
    }

    private async Task<SavedState> GetStateAsync(Guid userId)
    {
        var key = UserDataStoreService.BuildUserKey(SavedRootKey, userId);
        return await _store.GetAsync(SavedCategory, key, new SavedState());
    }

    private async Task SaveStateAsync(Guid userId, SavedState state)
    {
        var key = UserDataStoreService.BuildUserKey(SavedRootKey, userId);
        await _store.SaveAsync(SavedCategory, key, state, "Saved items and moodboards");
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
        public List<SavedBoardState> Boards { get; set; } = new();
    }

    private class SavedItemState
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public DateTime SavedAt { get; set; }
    }

    private class SavedBoardState
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public List<Guid> ItemIds { get; set; } = new();
    }

    private class SavedRow
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Category { get; set; }
        public decimal Price { get; set; }
        public string? Image { get; set; }
        public DateTime SavedAt { get; set; }
    }

    public class SaveItemRequest
    {
        public Guid ItemId { get; set; }
    }

    public class AddSavedToCartRequest
    {
        public int Quantity { get; set; } = 1;
    }

    public class AddToMoodboardRequest
    {
        public Guid? BoardId { get; set; }
    }

    public class CreateBoardRequest
    {
        public List<Guid> ItemIds { get; set; } = new();
        public string BoardName { get; set; } = string.Empty;
    }

    public class BuyAllRequest
    {
        public List<Guid> ItemIds { get; set; } = new();
    }
}
