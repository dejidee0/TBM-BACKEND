using TBM.Core.Enums;

namespace TBM.Application.DTOs.AI;

public class AIProjectResponseDto
{
    public Guid Id { get; set; }
    public AIProjectStatus Status { get; set; }
    public AIGenerationType GenerationType { get; set; }
    public AIOutputType OutputType { get; set; }
    public string SourceImageUrl { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string? ContextLabel { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class AIGenerationResultDto
{
    public Guid DesignId { get; set; }
    public Guid ProjectId { get; set; }
    public AIOutputType OutputType { get; set; }
    public string OutputUrl { get; set; } = string.Empty;
    public int? Width { get; set; }
    public int? Height { get; set; }
    public double? DurationSeconds { get; set; }
    public string? Provider { get; set; }
    public string? ProviderJobId { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Bogat inventory products matched to the materials/style in this design.
    /// Each item can be added to cart directly using its ProductId.
    /// </summary>
    public List<MatchedProductDto> MatchedProducts { get; set; } = new();
}

public class MatchedProductDto
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? MaterialType { get; set; }
    public string? Category { get; set; }
    public decimal? Price { get; set; }
    public string PriceDisplay { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string Slug { get; set; } = string.Empty;
    public bool InStock { get; set; }
}

public class AIUploadRoomResponseDto
{
    public bool Success { get; set; } = true;
    public string ImageUrl { get; set; } = string.Empty;
}
