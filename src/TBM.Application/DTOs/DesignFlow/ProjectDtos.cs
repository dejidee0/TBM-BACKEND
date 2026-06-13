using Microsoft.AspNetCore.Http;

namespace TBM.Application.DTOs.DesignFlow;

public class ProjectSummaryDto
{
    public Guid Id { get; set; }
    public string ProjectNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RoomType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? ExpectedCompletionDate { get; set; }
    public DateTime? ActualCompletionDate { get; set; }
    public decimal TotalBudget { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal AmountPending { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ProjectFinancialDto
{
    public decimal TotalBudget { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal AmountPending { get; set; }
}

public class ProjectTimelineDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string MilestoneName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime? PlannedDate { get; set; }
    public DateTime? ActualDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public class ProjectDocumentDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public Guid UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class ProjectGalleryImageDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string Thumbnail { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public Guid UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; }
    public int SortOrder { get; set; }
}

public class ProjectDto
{
    public Guid Id { get; set; }
    public string ProjectNumber { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public Guid DesignSessionId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? BOMId { get; set; }
    public Guid? VendorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RoomType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? ExpectedCompletionDate { get; set; }
    public DateTime? ActualCompletionDate { get; set; }
    public ProjectFinancialDto Financial { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ProjectDetailDto
{
    public ProjectDto Project { get; set; } = new();
    public List<ProjectTimelineDto> Timeline { get; set; } = new();
    public List<BomItemDto> Materials { get; set; } = new();
    public ProjectFinancialDto Financial { get; set; } = new();
    public object? Vendor { get; set; }
}

public class ProjectListDto
{
    public List<ProjectDto> Projects { get; set; } = new();
}

public class UploadProjectDocumentRequestDto
{
    public IFormFile File { get; set; } = null!;
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class UploadProjectDocumentResponseDto
{
    public Guid DocumentId { get; set; }
    public string FileUrl { get; set; } = string.Empty;
}

public class UploadProjectGalleryImageRequestDto
{
    public IFormFile Image { get; set; } = null!;
    public string? Caption { get; set; }
}

public class UploadProjectGalleryImageResponseDto
{
    public Guid ImageId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
}
