using System.Text;
using Microsoft.Extensions.Logging;
using TBM.Application.DTOs.DesignFlow;
using TBM.Application.Interfaces;
using TBM.Core.Entities.DesignFlow;
using TBM.Core.Enums;
using TBM.Core.Interfaces;
using TBM.Core.Entities.Products;

namespace TBM.Application.Services.DesignFlow;

public class BOMGenerationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBomGenerationClient _bomGenerationClient;
    private readonly ILogger<BOMGenerationService> _logger;

    public BOMGenerationService(
        IUnitOfWork unitOfWork,
        IBomGenerationClient bomGenerationClient,
        ILogger<BOMGenerationService> logger)
    {
        _unitOfWork = unitOfWork;
        _bomGenerationClient = bomGenerationClient;
        _logger = logger;
    }

    public async Task<List<BomInventoryProductDto>> GetEligibleInventoryAsync(
        DesignSessionTier tier,
        CancellationToken cancellationToken = default)
    {
        var products = await _unitOfWork.Products.GetInventoryCandidatesAsync();
        var filtered = products
            .Where(product => MatchesTier(product.QualityTier, tier))
            .ToList();

        if (!filtered.Any())
        {
            filtered = products;
        }

        return filtered.Select(MapInventoryProduct).ToList();
    }

    public async Task<BillOfMaterials?> GenerateBOMFromInventoryAsync(
        DesignSession session,
        CancellationToken cancellationToken = default)
    {
        var inventory = await GetEligibleInventoryAsync(session.Tier, cancellationToken);
        if (!inventory.Any())
        {
            _logger.LogWarning("No inventory candidates were found for design session {SessionId}", session.Id);
            return null;
        }

        var request = new BomGenerationRequestDto
        {
            SessionId = session.Id,
            ProjectName = session.ProjectName,
            RoomType = session.RoomType,
            VisionText = session.VisionText,
            Tier = session.Tier.ToString(),
            RoomLength = session.RoomLength,
            RoomWidth = session.RoomWidth,
            RoomHeight = session.RoomHeight,
            Inventory = inventory
        };

        var aiResponse = await _bomGenerationClient.GenerateAsync(request, cancellationToken);
        var items = BuildBomItems(session, inventory, aiResponse);

        if (!items.Any())
        {
            items = BuildFallbackItems(session, inventory);
        }

        if (!items.Any())
        {
            _logger.LogWarning("Unable to generate BOM items for design session {SessionId}", session.Id);
            return null;
        }

        var bom = new BillOfMaterials
        {
            DesignSessionId = session.Id,
            BomNumber = await _unitOfWork.BillsOfMaterials.GenerateBomNumberAsync(),
            Status = BillOfMaterialsStatus.Finalized
        };

        foreach (var item in items)
        {
            bom.Items.Add(item);
        }

        bom.ItemCount = bom.Items.Count;
        bom.TotalEstimatedCost = CalculateBOMCostAsync(bom.Items);

        await _unitOfWork.BillsOfMaterials.CreateAsync(bom);
        return bom;
    }

    public async Task<bool> ValidateBOMStockAsync(BillOfMaterials bom, CancellationToken cancellationToken = default)
    {
        foreach (var item in bom.Items)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId);
            if (product == null || !product.IsActive)
            {
                return false;
            }

            if (!product.TrackInventory || !product.StockQuantity.HasValue)
            {
                continue;
            }

            if (product.StockQuantity.Value < item.Quantity)
            {
                return false;
            }
        }

        return true;
    }

    public decimal CalculateBOMCostAsync(IEnumerable<BOMItem> items)
    {
        return items.Sum(item => item.TotalPrice);
    }

    private List<BOMItem> BuildBomItems(
        DesignSession session,
        List<BomInventoryProductDto> inventory,
        BomGenerationResultDto? aiResponse)
    {
        if (aiResponse?.Items == null || aiResponse.Items.Count == 0)
        {
            return new List<BOMItem>();
        }

        var inventoryMap = inventory
            .Where(product => !string.IsNullOrWhiteSpace(product.SKU))
            .ToDictionary(product => product.SKU, StringComparer.OrdinalIgnoreCase);

        var selected = new List<BOMItem>();

        foreach (var suggestion in aiResponse.Items
            .Where(item => !string.IsNullOrWhiteSpace(item.SKU))
            .GroupBy(item => item.SKU.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First()))
        {
            if (!inventoryMap.TryGetValue(suggestion.SKU, out var product))
            {
                _logger.LogWarning(
                    "Rejected hallucinated BOM SKU {Sku} for design session {SessionId}",
                    suggestion.SKU,
                    session.Id);
                continue;
            }

            var quantity = DetermineQuantity(session, product, suggestion.Quantity);
            if (quantity <= 0)
            {
                continue;
            }

            var maxAvailable = product.StockQuantity.HasValue
                ? (decimal)product.StockQuantity.Value
                : quantity;
            if (maxAvailable < quantity)
            {
                quantity = maxAvailable;
            }

            if (quantity <= 0)
            {
                continue;
            }

            selected.Add(CreateBomItem(product, quantity, suggestion.Reason, suggestion.LeadTimeDays));
        }

        return selected;
    }

    private List<BOMItem> BuildFallbackItems(
        DesignSession session,
        List<BomInventoryProductDto> inventory)
    {
        var scored = inventory
            .Select(product => new
            {
                Product = product,
                Score = ScoreProduct(session, product)
            })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Product.StockQuantity ?? 0)
            .Take(8)
            .ToList();

        var items = new List<BOMItem>();
        foreach (var entry in scored)
        {
            var quantity = DetermineHeuristicQuantity(session, entry.Product);
            if (quantity <= 0)
            {
                quantity = 1;
            }

            var available = entry.Product.StockQuantity.HasValue
                ? (decimal)entry.Product.StockQuantity.Value
                : quantity;
            if (available < quantity)
            {
                quantity = available;
            }

            if (quantity <= 0)
            {
                continue;
            }

            items.Add(CreateBomItem(
                entry.Product,
                quantity,
                $"Selected as fallback for {session.RoomType.ToLowerInvariant()} inventory fit.",
                null));
        }

        return items;
    }

    private static BOMItem CreateBomItem(
        BomInventoryProductDto product,
        decimal quantity,
        string reason,
        int? leadTimeDays)
    {
        var unitPrice = product.Price ?? 0m;
        var normalizedQuantity = decimal.Round(quantity, 0, MidpointRounding.AwayFromZero);
        return new BOMItem
        {
            ProductId = product.ProductId,
            SKU = product.SKU,
            Name = product.Name,
            Description = product.Description,
            Quantity = normalizedQuantity,
            UnitPrice = unitPrice,
            TotalPrice = decimal.Round(unitPrice * normalizedQuantity, 2, MidpointRounding.AwayFromZero),
            InStock = (product.StockQuantity ?? 0) > 0,
            VendorId = null,
            LeadTimeDays = leadTimeDays,
            Reason = string.IsNullOrWhiteSpace(reason)
                ? "Selected from inventory"
                : reason.Trim()
        };
    }

    private static BomInventoryProductDto MapInventoryProduct(Product product)
    {
        return new BomInventoryProductDto
        {
            ProductId = product.Id,
            SKU = product.SKU ?? string.Empty,
            Name = product.Name,
            Description = product.Description,
            AIKeywords = product.AIKeywords,
            MaterialType = product.MaterialType,
            QualityTier = product.QualityTier,
            RecommendedFor = product.RecommendedFor,
            Price = product.Price,
            StockQuantity = product.StockQuantity,
            CategoryName = product.Category?.Name
        };
    }

    private static BomGenerationRequestDto BuildRequest(
        DesignSession session,
        List<BomInventoryProductDto> inventory)
    {
        return new BomGenerationRequestDto
        {
            SessionId = session.Id,
            ProjectName = session.ProjectName,
            RoomType = session.RoomType,
            VisionText = session.VisionText,
            Tier = session.Tier.ToString(),
            RoomLength = session.RoomLength,
            RoomWidth = session.RoomWidth,
            RoomHeight = session.RoomHeight,
            Inventory = inventory
        };
    }

    private static bool MatchesTier(string? qualityTier, DesignSessionTier tier)
    {
        if (string.IsNullOrWhiteSpace(qualityTier))
        {
            return true;
        }

        return tier switch
        {
            DesignSessionTier.Luxury => qualityTier.Contains("premium", StringComparison.OrdinalIgnoreCase) ||
                                        qualityTier.Contains("luxury", StringComparison.OrdinalIgnoreCase),
            DesignSessionTier.Economic => qualityTier.Contains("budget", StringComparison.OrdinalIgnoreCase) ||
                                          qualityTier.Contains("standard", StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }

    private static decimal DetermineQuantity(
        DesignSession session,
        BomInventoryProductDto product,
        decimal requestedQuantity)
    {
        var heuristicQuantity = DetermineHeuristicQuantity(session, product);
        var quantity = Math.Max(1m, requestedQuantity);

        if (heuristicQuantity > quantity)
        {
            quantity = heuristicQuantity;
        }

        return quantity;
    }

    private static decimal DetermineHeuristicQuantity(DesignSession session, BomInventoryProductDto product)
    {
        var text = string.Join(" ",
            product.Name,
            product.Description,
            product.AIKeywords,
            product.MaterialType,
            product.RecommendedFor,
            product.CategoryName).ToLowerInvariant();

        var area = Math.Max(1m, session.RoomLength * session.RoomWidth);
        var perimeter = Math.Max(1m, 2m * (session.RoomLength + session.RoomWidth));
        var wallArea = Math.Max(1m, perimeter * session.RoomHeight);

        if (text.Contains("tile", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("floor", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Ceiling(area * 1.15m);
        }

        if (text.Contains("paint", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Ceiling(wallArea / 10m);
        }

        if (text.Contains("light", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("lamp", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Max(2m, Math.Ceiling(area / 15m));
        }

        if (text.Contains("fixture", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("cabinet", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("wardrobe", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("wc", StringComparison.OrdinalIgnoreCase))
        {
            return 1m;
        }

        return Math.Max(1m, Math.Ceiling(area / 8m));
    }

    private static int ScoreProduct(DesignSession session, BomInventoryProductDto product)
    {
        var query = string.Join(" ",
            session.RoomType,
            session.VisionText,
            session.ProjectName,
            product.Name,
            product.Description,
            product.AIKeywords,
            product.MaterialType,
            product.RecommendedFor,
            product.CategoryName).ToLowerInvariant();

        var score = 0;
        score += CountMatches(query, "tile", "floor", "stone", "marble") * 5;
        score += CountMatches(query, "paint", "color", "wall") * 4;
        score += CountMatches(query, "wc", "toilet", "bath", "bathroom") * 4;
        score += CountMatches(query, "kitchen", "cabinet", "sink") * 4;
        score += CountMatches(query, "bedroom", "wardrobe", "closet") * 3;
        score += CountMatches(query, "premium", "luxury", "budget", "standard") * 2;

        if (!string.IsNullOrWhiteSpace(product.QualityTier))
        {
            score += 1;
        }

        if (product.StockQuantity.HasValue)
        {
            score += Math.Min(product.StockQuantity.Value / 10, 5);
        }

        return score;
    }

    private static int CountMatches(string text, params string[] terms)
    {
        return terms.Count(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
