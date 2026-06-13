using TBM.Core.Entities.Common;
using TBM.Core.Enums;

namespace TBM.Core.Entities.DesignFlow;

public class Project : AuditableEntity
{
    public string ProjectNumber { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public Guid DesignSessionId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? BOMId { get; set; }
    public Guid? VendorId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RoomType { get; set; } = string.Empty;
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;

    public DateTime StartDate { get; set; }
    public DateTime? ExpectedCompletionDate { get; set; }
    public DateTime? ActualCompletionDate { get; set; }

    public decimal TotalBudget { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal AmountPending { get; set; }

    public ICollection<ProjectTimeline> Timelines { get; set; } = new List<ProjectTimeline>();
    public ICollection<ProjectDocument> Documents { get; set; } = new List<ProjectDocument>();
    public ICollection<SiteGalleryImage> GalleryImages { get; set; } = new List<SiteGalleryImage>();
}
