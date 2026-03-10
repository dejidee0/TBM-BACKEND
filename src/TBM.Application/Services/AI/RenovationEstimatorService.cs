using TBM.Application.DTOs.AI;
using TBM.Core.Entities.AI;
using TBM.Core.Interfaces;

namespace TBM.Application.Services;

public class RenovationEstimatorService
{
    private readonly IUnitOfWork _unitOfWork;

    public RenovationEstimatorService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<RenovationEstimateResponseDto> CreateEstimateAsync(
        Guid userId,
        CreateRenovationEstimateRequestDto request)
    {
        ValidateRequest(request);

        var finishLevel = NormalizeFinishLevel(request.FinishLevel);
        var floorArea = Round(request.LengthMeters * request.WidthMeters, 3);
        var wallArea = Round((2m * (request.LengthMeters + request.WidthMeters)) * request.HeightMeters, 3);
        var contingencyPercent = Math.Clamp(request.ContingencyPercent, 0m, 30m);

        var lineItems = BuildLineItems(request, finishLevel, floorArea, wallArea);
        var materialsSubtotal = Round(lineItems
            .Where(x => string.Equals(x.Group, "Materials", StringComparison.OrdinalIgnoreCase))
            .Sum(x => x.TotalCost));
        var laborSubtotal = Round(lineItems
            .Where(x => string.Equals(x.Group, "Labor", StringComparison.OrdinalIgnoreCase))
            .Sum(x => x.TotalCost));
        var subTotal = Round(materialsSubtotal + laborSubtotal);
        var contingencyAmount = Round(subTotal * (contingencyPercent / 100m));
        var totalEstimate = Round(subTotal + contingencyAmount);

        var suggestions = await BuildSuggestedProductsAsync(request.RoomType);
        var estimateNumber = await _unitOfWork.AIRenovationEstimates.GenerateEstimateNumberAsync();

        var entity = new AIRenovationEstimate
        {
            UserId = userId,
            EstimateNumber = estimateNumber,
            ProjectName = request.ProjectName.Trim(),
            RoomType = request.RoomType.Trim(),
            LengthMeters = Round(request.LengthMeters, 3),
            WidthMeters = Round(request.WidthMeters, 3),
            HeightMeters = Round(request.HeightMeters, 3),
            FinishLevel = finishLevel,
            IncludeFlooring = request.IncludeFlooring,
            IncludePainting = request.IncludePainting,
            IncludeElectrical = request.IncludeElectrical,
            IncludePlumbing = request.IncludePlumbing,
            ContingencyPercent = Round(contingencyPercent, 3),
            FloorAreaSqm = floorArea,
            WallAreaSqm = wallArea,
            Currency = "NGN",
            MaterialsSubtotal = materialsSubtotal,
            LaborSubtotal = laborSubtotal,
            ContingencyAmount = contingencyAmount,
            TotalEstimate = totalEstimate,
            Summary = BuildSummary(finishLevel, floorArea, wallArea, totalEstimate)
        };

        foreach (var item in lineItems)
        {
            entity.LineItems.Add(new AIRenovationEstimateLineItem
            {
                Group = item.Group,
                Name = item.Name,
                Quantity = Round(item.Quantity, 3),
                Unit = item.Unit,
                UnitCost = Round(item.UnitCost),
                TotalCost = Round(item.TotalCost)
            });
        }

        foreach (var suggestion in suggestions)
        {
            entity.SuggestedProducts.Add(new AIRenovationEstimateSuggestedProduct
            {
                ProductId = suggestion.ProductId,
                Name = suggestion.Name,
                Price = suggestion.Price,
                Category = suggestion.Category,
                Link = suggestion.Link
            });
        }

        await _unitOfWork.AIRenovationEstimates.CreateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return MapResponse(entity);
    }

    public async Task<List<RenovationEstimateSummaryDto>> GetEstimatesAsync(Guid userId)
    {
        var estimates = await _unitOfWork.AIRenovationEstimates.GetByUserAsync(userId);
        return estimates.Select(x => new RenovationEstimateSummaryDto
        {
            EstimateId = x.Id,
            ProjectName = x.ProjectName,
            RoomType = x.RoomType,
            CreatedAtUtc = x.CreatedAt,
            TotalEstimate = x.TotalEstimate,
            Currency = x.Currency
        }).ToList();
    }

    public async Task<RenovationEstimateResponseDto?> GetEstimateByIdAsync(Guid userId, Guid estimateId)
    {
        var estimate = await _unitOfWork.AIRenovationEstimates.GetByIdAsync(estimateId, userId);
        return estimate == null ? null : MapResponse(estimate);
    }

    private static RenovationEstimateResponseDto MapResponse(AIRenovationEstimate estimate)
    {
        return new RenovationEstimateResponseDto
        {
            EstimateId = estimate.Id,
            ProjectName = estimate.ProjectName,
            RoomType = estimate.RoomType,
            CreatedAtUtc = estimate.CreatedAt,
            FloorAreaSqm = estimate.FloorAreaSqm,
            WallAreaSqm = estimate.WallAreaSqm,
            Currency = estimate.Currency,
            FinishLevel = estimate.FinishLevel,
            MaterialsSubtotal = estimate.MaterialsSubtotal,
            LaborSubtotal = estimate.LaborSubtotal,
            ContingencyAmount = estimate.ContingencyAmount,
            TotalEstimate = estimate.TotalEstimate,
            Summary = estimate.Summary,
            LineItems = estimate.LineItems
                .OrderBy(x => x.CreatedAt)
                .Select(x => new RenovationEstimateLineItemDto
                {
                    Group = x.Group,
                    Name = x.Name,
                    Quantity = x.Quantity,
                    Unit = x.Unit,
                    UnitCost = x.UnitCost,
                    TotalCost = x.TotalCost
                })
                .ToList(),
            SuggestedProducts = estimate.SuggestedProducts
                .OrderBy(x => x.CreatedAt)
                .Select(x => new RenovationEstimateSuggestedProductDto
                {
                    ProductId = x.ProductId ?? Guid.Empty,
                    Name = x.Name,
                    Price = x.Price,
                    Category = x.Category,
                    Link = x.Link
                })
                .ToList(),
            NextSteps = BuildNextSteps()
        };
    }

    private static void ValidateRequest(CreateRenovationEstimateRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectName))
        {
            throw new InvalidOperationException("Project name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.RoomType))
        {
            throw new InvalidOperationException("Room type is required.");
        }

        if (request.LengthMeters <= 0 || request.WidthMeters <= 0 || request.HeightMeters <= 0)
        {
            throw new InvalidOperationException("Length, width, and height must be greater than zero.");
        }
    }

    private static string NormalizeFinishLevel(string? finishLevel)
    {
        var normalized = finishLevel?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "basic" => "basic",
            "premium" => "premium",
            _ => "standard"
        };
    }

    private static List<RenovationEstimateLineItemDto> BuildLineItems(
        CreateRenovationEstimateRequestDto request,
        string finishLevel,
        decimal floorArea,
        decimal wallArea)
    {
        var factor = finishLevel switch
        {
            "basic" => 0.85m,
            "premium" => 1.35m,
            _ => 1.0m
        };

        var lineItems = new List<RenovationEstimateLineItemDto>();

        if (request.IncludeFlooring)
        {
            AddLine(lineItems, "Materials", "Flooring materials", floorArea, "sqm", Round(23000m * factor));
            AddLine(lineItems, "Labor", "Flooring installation", floorArea, "sqm", Round(9000m * factor));
        }

        if (request.IncludePainting)
        {
            AddLine(lineItems, "Materials", "Wall paint and prep materials", wallArea, "sqm", Round(4200m * factor));
            AddLine(lineItems, "Labor", "Painting labor", wallArea, "sqm", Round(3000m * factor));
        }

        if (request.IncludeElectrical)
        {
            AddLine(lineItems, "Labor", "Electrical fittings and labor allowance", floorArea, "sqm", Round(6500m * factor));
        }

        if (request.IncludePlumbing)
        {
            AddLine(lineItems, "Labor", "Plumbing fittings and labor allowance", floorArea, "sqm", Round(7000m * factor));
        }

        if (!lineItems.Any())
        {
            AddLine(lineItems, "Labor", "General renovation planning allowance", floorArea, "sqm", Round(5000m * factor));
        }

        return lineItems;
    }

    private static void AddLine(
        List<RenovationEstimateLineItemDto> items,
        string group,
        string name,
        decimal quantity,
        string unit,
        decimal unitCost)
    {
        items.Add(new RenovationEstimateLineItemDto
        {
            Group = group,
            Name = name,
            Quantity = quantity,
            Unit = unit,
            UnitCost = unitCost,
            TotalCost = Round(quantity * unitCost)
        });
    }

    private async Task<List<RenovationEstimateSuggestedProductDto>> BuildSuggestedProductsAsync(string roomType)
    {
        var (items, _) = await _unitOfWork.Products.GetPagedAsync(
            pageNumber: 1,
            pageSize: 20,
            searchTerm: roomType,
            activeOnly: true);

        var selected = items
            .Where(x => x.Price.HasValue)
            .Take(6)
            .ToList();

        if (!selected.Any())
        {
            var (fallback, _) = await _unitOfWork.Products.GetPagedAsync(
                pageNumber: 1,
                pageSize: 6,
                activeOnly: true);
            selected = fallback.ToList();
        }

        return selected.Select(x => new RenovationEstimateSuggestedProductDto
        {
            ProductId = x.Id,
            Name = x.Name,
            Price = x.Price,
            Category = x.Category?.Name,
            Link = $"/materials/{x.Id}"
        }).ToList();
    }

    private static List<RenovationEstimateLinkDto> BuildNextSteps()
    {
        return new List<RenovationEstimateLinkDto>
        {
            new()
            {
                Label = "Browse recommended materials",
                Url = "/materials",
                Method = "GET"
            },
            new()
            {
                Label = "Book consultation",
                Url = "/api/dashboard/consultations",
                Method = "GET"
            },
            new()
            {
                Label = "Start checkout when ready",
                Url = "/api/checkout",
                Method = "GET"
            }
        };
    }

    private static string BuildSummary(
        string finishLevel,
        decimal floorArea,
        decimal wallArea,
        decimal totalEstimate)
    {
        return $"Estimated {finishLevel} renovation for {floorArea:N2}sqm floor and {wallArea:N2}sqm wall area: NGN {totalEstimate:N2}.";
    }

    private static decimal Round(decimal value, int decimals = 2)
    {
        return Math.Round(value, decimals, MidpointRounding.AwayFromZero);
    }
}
