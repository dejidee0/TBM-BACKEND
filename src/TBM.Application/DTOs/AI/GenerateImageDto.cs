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
    /// <summary>
    /// Optional style preset ID (see GET /api/v1/ai/styles). Falls back to
    /// "afro-minimalism" when omitted or unrecognized.
    /// </summary>
    public string? Style { get; set; }
}
