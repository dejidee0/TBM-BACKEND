namespace TBM.Application.DTOs.AI;

public class GenerateImageDto
{
    public Guid ProjectId { get; set; }
    public string? Prompt { get; set; }
    public string? SourceImageUrl { get; set; }
    /// <summary>
    /// Optional context tags to guide the AI (e.g. "Modern Living Room", "Marble Flooring").
    /// Predefined categories: Interior Design types, Construction types, Furniture types, Materials.
    /// </summary>
    public List<string>? ContextTags { get; set; }
}
