using TBM.Core.Entities.Common;

namespace TBM.Core.Entities.AI;

public class AIAssistantSession : AuditableEntity
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime LastUpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<AIAssistantMessage> Messages { get; set; } = new List<AIAssistantMessage>();
    public ICollection<AIAssistantTask> Tasks { get; set; } = new List<AIAssistantTask>();
    public ICollection<AIAssistantToolAction> ToolActions { get; set; } = new List<AIAssistantToolAction>();
}
