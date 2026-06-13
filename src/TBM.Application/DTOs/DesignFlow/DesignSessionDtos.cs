using Microsoft.AspNetCore.Http;
using TBM.Core.Enums;

namespace TBM.Application.DTOs.DesignFlow;

public class RoomDimensionsDto
{
    public decimal Length { get; set; }
    public decimal Width { get; set; }
    public decimal Height { get; set; }
}

public class CreateDesignSessionRequestDto
{
    public string ProjectName { get; set; } = string.Empty;
    public string RoomType { get; set; } = string.Empty;
    public string VisionText { get; set; } = string.Empty;
    public DesignSessionTier Tier { get; set; }
    public RoomDimensionsDto RoomDimensions { get; set; } = new();
}

public class CreateDesignSessionResponseDto
{
    public Guid SessionId { get; set; }
    public string SessionNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string UploadUrl { get; set; } = string.Empty;
}

public class UploadDesignSessionPhotoRequestDto
{
    public IFormFile Image { get; set; } = null!;
}

public class UploadDesignSessionPhotoResponseDto
{
    public string OriginalImageUrl { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class GenerateDesignSessionRequestDto
{
    public bool GenerateBOM { get; set; } = true;
}

public class GenerateDesignSessionResponseDto
{
    public string Status { get; set; } = "Processing";
    public int EstimatedTime { get; set; } = 45;
    public string StatusUrl { get; set; } = string.Empty;
}

public class DesignSessionStatusDto
{
    public string Status { get; set; } = string.Empty;
    public int Progress { get; set; }
    public string? CurrentStep { get; set; }
    public string? ImageUrl { get; set; }
    public bool BomGenerated { get; set; }
    public string? ErrorMessage { get; set; }
}

public class DesignSessionSummaryDto
{
    public Guid SessionId { get; set; }
    public string SessionNumber { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string RoomType { get; set; } = string.Empty;
    public DesignSessionTier Tier { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Progress { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class DesignSessionListDto
{
    public List<DesignSessionSummaryDto> Sessions { get; set; } = new();
}

public class AddDesignSessionToCartRequestDto
{
    public bool AddAll { get; set; } = true;
    public List<Guid> ItemIds { get; set; } = new();
}

public class AddDesignSessionToCartResponseDto
{
    public Guid CartId { get; set; }
    public int ItemsAdded { get; set; }
    public decimal TotalAmount { get; set; }
}

public class DesignSessionDto
{
    public Guid SessionId { get; set; }
    public string SessionNumber { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string RoomType { get; set; } = string.Empty;
    public string VisionText { get; set; } = string.Empty;
    public DesignSessionTier Tier { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Progress { get; set; }
    public string? CurrentStep { get; set; }
    public string? ErrorMessage { get; set; }
    public string? OriginalImageUrl { get; set; }
    public string? GeneratedImageUrl { get; set; }
    public decimal RoomLength { get; set; }
    public decimal RoomWidth { get; set; }
    public decimal RoomHeight { get; set; }
    public Guid? BOMId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? ProjectId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class BomItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public bool InStock { get; set; }
    public Guid? VendorId { get; set; }
    public int? LeadTimeDays { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class BillOfMaterialsDto
{
    public Guid Id { get; set; }
    public Guid DesignSessionId { get; set; }
    public string BomNumber { get; set; } = string.Empty;
    public decimal TotalEstimatedCost { get; set; }
    public int ItemCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<BomItemDto> Items { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class DesignSessionDetailDto
{
    public DesignSessionDto Session { get; set; } = new();
    public BillOfMaterialsDto? BillOfMaterials { get; set; }
    public bool AllItemsInStock { get; set; }
}
