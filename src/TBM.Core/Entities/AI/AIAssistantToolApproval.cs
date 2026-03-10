using TBM.Core.Entities.Common;
using TBM.Core.Enums;

namespace TBM.Core.Entities.AI;

public class AIAssistantToolApproval : AuditableEntity
{
    public Guid ToolActionId { get; set; }
    public Guid UserId { get; set; }
    public AIAssistantApprovalStatus Status { get; set; } = AIAssistantApprovalStatus.Pending;
    public string? Reason { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }

    public AIAssistantToolAction ToolAction { get; set; } = null!;
}
