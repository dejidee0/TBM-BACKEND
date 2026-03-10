using TBM.Core.Entities.Common;
using TBM.Core.Enums;

namespace TBM.Core.Entities.AI;

public class AIAssistantMessage : AuditableEntity
{
    public Guid SessionId { get; set; }
    public AIAssistantMessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? Intent { get; set; }
    public string? LinksJson { get; set; }

    public AIAssistantSession Session { get; set; } = null!;
    public ICollection<AIAssistantToolAction> ToolActions { get; set; } = new List<AIAssistantToolAction>();
}
