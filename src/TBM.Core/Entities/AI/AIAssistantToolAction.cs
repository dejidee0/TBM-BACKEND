using TBM.Core.Entities.Common;
using TBM.Core.Enums;

namespace TBM.Core.Entities.AI;

public class AIAssistantToolAction : AuditableEntity
{
    public Guid SessionId { get; set; }
    public Guid? MessageId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ActionUrl { get; set; } = string.Empty;
    public string ActionMethod { get; set; } = "GET";
    public string? PayloadJson { get; set; }
    public bool RequiresApproval { get; set; }
    public AIAssistantToolActionStatus Status { get; set; }

    public AIAssistantSession Session { get; set; } = null!;
    public AIAssistantMessage? Message { get; set; }
    public ICollection<AIAssistantToolApproval> Approvals { get; set; } = new List<AIAssistantToolApproval>();
    public ICollection<AIAssistantToolExecution> Executions { get; set; } = new List<AIAssistantToolExecution>();
}
