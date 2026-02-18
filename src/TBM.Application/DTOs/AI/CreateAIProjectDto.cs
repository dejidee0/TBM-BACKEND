using TBM.Core.Enums;

public class CreateAIProjectDto
{
    public string SourceImageUrl { get; set; } = null!;
    public AIGenerationType GenerationType { get; set; }
    public string? Prompt { get; set; }
    public string? ContextLabel { get; set; }
}
