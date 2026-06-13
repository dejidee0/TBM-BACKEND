using TBM.Core.Entities.AI;
using TBM.Core.Entities.Common;
using TBM.Core.Enums;

public class AIProject : AuditableEntity
{
   
    public Guid UserId { get; set; }

    // Core input
    public string SourceImageUrl { get; set; } = null!;

    // AI intent
    public AIGenerationType GenerationType { get; set; }
    public AIProjectStatus Status { get; set; }

    // Prompt context (flexible)
    public string? Prompt { get; set; }
    public string? NegativePrompt { get; set; }

    // Optional metadata (JSON-safe later)
    public string? ContextLabel { get; set; } // e.g. "kitchen", "office", "product"
public ICollection<AIDesign> Designs { get; set; } = new List<AIDesign>();

}
