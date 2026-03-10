using TBM.Core.Entities.Common;
using TBM.Core.Enums;

namespace TBM.Core.Entities.AI;

public class AIAssistantTask : AuditableEntity
{
    public Guid SessionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AIAssistantTaskStatus Status { get; set; } = AIAssistantTaskStatus.Pending;
    public string? ActionUrl { get; set; }
    public string ActionMethod { get; set; } = "GET";
    public bool RequiresApproval { get; set; }
    public Guid? ToolActionId { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public AIAssistantSession Session { get; set; } = null!;
    public AIAssistantToolAction? ToolAction { get; set; }
}
