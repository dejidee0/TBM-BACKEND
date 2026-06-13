using TBM.Core.Entities.Common;
using TBM.Core.Enums;

namespace TBM.Core.Entities.DesignFlow;

public class DesignSession : AuditableEntity
{
    public Guid UserId { get; set; }

    public string SessionNumber { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string RoomType { get; set; } = string.Empty;
    public string VisionText { get; set; } = string.Empty;
    public DesignSessionTier Tier { get; set; }

    public string? OriginalImageUrl { get; set; }
    public string? GeneratedImageUrl { get; set; }

    public DesignSessionStatus Status { get; set; } = DesignSessionStatus.Draft;

    public decimal RoomLength { get; set; }
    public decimal RoomWidth { get; set; }
    public decimal RoomHeight { get; set; }

    public int Progress { get; set; }
    public string? CurrentStep { get; set; }
    public string? ErrorMessage { get; set; }

    public Guid? BOMId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? ProjectId { get; set; }
}
