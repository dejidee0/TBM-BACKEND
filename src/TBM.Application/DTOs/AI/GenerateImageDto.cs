namespace TBM.Application.DTOs.AI;

public class GenerateImageDto
{
    public Guid ProjectId { get; set; }
    public string Prompt { get; set; } = default!;
    public string SourceImageUrl { get; set; } = default!;
}
