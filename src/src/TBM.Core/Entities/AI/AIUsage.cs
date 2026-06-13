using TBM.Core.Entities.Common;
using TBM.Core.Enums;

namespace TBM.Core.Entities.AI;

public class AIUsage : AuditableEntity
{
  

    public Guid UserId { get; set; }
    public Guid AIProjectId { get; set; }

    public AIGenerationType GenerationType { get; set; }

    // Metrics
    public int CreditsUsed { get; set; }
    public decimal EstimatedCost { get; set; }

    public string? Provider { get; set; }
}
