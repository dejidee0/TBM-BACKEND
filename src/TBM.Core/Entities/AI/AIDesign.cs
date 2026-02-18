using System.Text.Json.Serialization;
using TBM.Core.Entities.Common;
using TBM.Core.Enums;

namespace TBM.Core.Entities.AI;

public class AIDesign : AuditableEntity
{
    public Guid AIProjectId { get; set; }

    public string OutputUrl { get; set; } = null!;
    public AIOutputType OutputType { get; set; }

    public int? Width { get; set; }
    public int? Height { get; set; }
    public double? DurationSeconds { get; set; }

    public string? Provider { get; set; }
    public string? ProviderJobId { get; set; }

 [JsonIgnore] // ✅ FIX: Prevent circular reference
    public AIProject AIProject { get; set; } = null!;
}
