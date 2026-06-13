using TBM.Core.Entities.Common;
using TBM.Core.Enums;

namespace TBM.Core.Entities.Portfolio;

public class VendorPortfolioProject : AuditableEntity
{
    public Guid VendorId { get; set; }
    public string VendorName { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;

    public decimal? BudgetMin { get; set; }
    public decimal? BudgetMax { get; set; }

    public int? DurationMinDays { get; set; }
    public int? DurationMaxDays { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>Newline-separated list of scope items (e.g. "Demolition\nWall painting\nFlooring")</summary>
    public string ScopeOfWork { get; set; } = string.Empty;

    public PortfolioStatus Status { get; set; } = PortfolioStatus.Draft;
    public string? RejectionReason { get; set; }
    public DateTime? PublishedAt { get; set; }

    public ICollection<PortfolioProjectImage> Images { get; set; } = new List<PortfolioProjectImage>();
}
