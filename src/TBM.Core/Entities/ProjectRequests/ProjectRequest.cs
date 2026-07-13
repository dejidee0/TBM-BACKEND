using TBM.Core.Entities.Common;

namespace TBM.Core.Entities.ProjectRequests;

public class ProjectRequest : AuditableEntity
{
    public Guid? UserId { get; set; }
    public Guid? EstimateId { get; set; }
    public string RequestType { get; set; } = string.Empty;
    public string? ProjectDescription { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string? AdditionalNotes { get; set; }
    public string Status { get; set; } = "Received";
}
