using TBM.Core.Entities.Common;
using TBM.Core.Enums;

namespace TBM.Core.Entities.AI;

public class AIAssistantToolExecution : AuditableEntity
{
    public Guid ToolActionId { get; set; }
    public Guid ExecutedByUserId { get; set; }
    public AIAssistantToolExecutionStatus Status { get; set; } = AIAssistantToolExecutionStatus.Succeeded;
    public string? ResultJson { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ExecutedAtUtc { get; set; } = DateTime.UtcNow;

    public AIAssistantToolAction ToolAction { get; set; } = null!;
}
