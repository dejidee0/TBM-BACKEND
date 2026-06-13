using TBM.Core.Enums;

namespace TBM.Application.DTOs.AI;

public class CreateAIProjectDto
{
    public string? SourceImageUrl { get; set; }

    // Preferred frontend-facing selector: Image or Video.
    public AIOutputType? OutputType { get; set; }

    // Backward-compatible internal selector. If both are supplied they must match.
    public AIGenerationType? GenerationType { get; set; }

    public string? Prompt { get; set; }
    public string? ContextLabel { get; set; }
}
