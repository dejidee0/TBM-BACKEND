using TBM.Core.Enums;

namespace TBM.Application.DTOs.AI;

public class PublicProjectListItemDto
{
    public Guid DesignId { get; set; }
    public Guid ProjectId { get; set; }
    public string OutputUrl { get; set; } = string.Empty;
    public AIOutputType OutputType { get; set; }
    public string? RoomType { get; set; }
    public string? Prompt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
}
