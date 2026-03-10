using TBM.Core.Enums;

namespace TBM.Application.DTOs.AI;

public class AIProjectListItemDto
{
    public Guid Id { get; set; }
    public AIProjectStatus Status { get; set; }
    public AIGenerationType GenerationType { get; set; }
    public string? ContextLabel { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? LatestDesignUrl { get; set; }
    public int DesignCount { get; set; }
}
