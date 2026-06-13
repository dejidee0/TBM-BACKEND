using TBM.Core.Entities.Common;
using TBM.Core.Enums;

namespace TBM.Core.Entities.DesignFlow;

public class ProjectTimeline : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public string MilestoneName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime? PlannedDate { get; set; }
    public DateTime? ActualDate { get; set; }
    public ProjectTimelineStatus Status { get; set; } = ProjectTimelineStatus.Pending;
    public int SortOrder { get; set; }
}
