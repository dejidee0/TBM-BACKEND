namespace TBM.Application.DTOs.AI;

public class CreateRenovationEstimateRequestDto
{
    public string ProjectName { get; set; } = "My Renovation";
    public string RoomType { get; set; } = "general";
    public decimal LengthMeters { get; set; }
    public decimal WidthMeters { get; set; }
    public decimal HeightMeters { get; set; } = 2.8m;
    public string FinishLevel { get; set; } = "standard";
    public bool IncludeFlooring { get; set; } = true;
    public bool IncludePainting { get; set; } = true;
    public bool IncludeElectrical { get; set; }
    public bool IncludePlumbing { get; set; }
    public decimal ContingencyPercent { get; set; } = 10m;

    // Backwards compatibility: some clients send a nested `roomDimensions` object
    // with { length, width, height } instead of the top-level meter properties.
    // When the nested object is present, populate the top-level values if they
    // were not provided (or are zero).
    private RoomDimensionsDto? _roomDimensions;
    public RoomDimensionsDto? RoomDimensions
    {
        get => _roomDimensions;
        set
        {
            _roomDimensions = value;
            if (value != null)
            {
                if (LengthMeters <= 0 && value.Length > 0) LengthMeters = value.Length;
                if (WidthMeters <= 0 && value.Width > 0) WidthMeters = value.Width;
                if (HeightMeters <= 0 && value.Height > 0) HeightMeters = value.Height;
            }
        }
    }
}

public class RoomDimensionsDto
{
    public decimal Length { get; set; }
    public decimal Width { get; set; }
    public decimal Height { get; set; }
}
public class RenovationEstimateSummaryDto
{
    public Guid EstimateId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string RoomType { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public decimal TotalEstimate { get; set; }
    public string Currency { get; set; } = "NGN";
}

public class RenovationEstimateResponseDto
{
    public Guid EstimateId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string RoomType { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public decimal FloorAreaSqm { get; set; }
    public decimal WallAreaSqm { get; set; }
    public string Currency { get; set; } = "NGN";
    public string FinishLevel { get; set; } = "standard";
    public decimal MaterialsSubtotal { get; set; }
    public decimal LaborSubtotal { get; set; }
    public decimal ContingencyAmount { get; set; }
    public decimal TotalEstimate { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Status { get; set; } = "Saved";
    public List<RenovationEstimateLineItemDto> LineItems { get; set; } = new();
    public List<RenovationEstimateSuggestedProductDto> SuggestedProducts { get; set; } = new();
    public List<RenovationEstimateLinkDto> NextSteps { get; set; } = new();
    public List<PaymentPlanOptionDto> PaymentPlanOptions { get; set; } = new();
}

public class PaymentPlanOptionDto
{
    public string Name { get; set; } = string.Empty;
    public int Installments { get; set; }
    public decimal PerInstallment { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class RenovationEstimateLineItemDto
{
    public string Group { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
}

public class RenovationEstimateSuggestedProductDto
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public string? Category { get; set; }
    public string Link { get; set; } = string.Empty;
}

public class RenovationEstimateLinkDto
{
    public string Label { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Method { get; set; } = "GET";
}
