namespace TBM.Application.DTOs.Inspiration;

public class InspirationDesignDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Style { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}
