using TBM.Core.Enums;

namespace TBM.Application.DTOs.Portfolio;

// ── REQUEST DTOs ────────────────────────────────────────────────────────────

public class CreatePortfolioProjectDto
{
    public string Title { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal? BudgetMin { get; set; }
    public decimal? BudgetMax { get; set; }
    public int? DurationMinDays { get; set; }
    public int? DurationMaxDays { get; set; }
    public string Description { get; set; } = string.Empty;
    /// <summary>List of scope items e.g. ["Demolition","Wall painting","Flooring"]</summary>
    public List<string> ScopeOfWork { get; set; } = new();
}

/// <summary>Admin-only: create a portfolio project on behalf of any vendor.</summary>
public class AdminCreatePortfolioProjectDto
{
    /// <summary>Display name for the vendor / company (e.g. "Mr Mike - Builder Barn").</summary>
    public string VendorName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal? BudgetMin { get; set; }
    public decimal? BudgetMax { get; set; }
    public int? DurationMinDays { get; set; }
    public int? DurationMaxDays { get; set; }
    public string Description { get; set; } = string.Empty;
    /// <summary>Each item is one scope-of-work line, e.g. ["Demolition","Wall painting"].</summary>
    public List<string> ScopeOfWork { get; set; } = new();
    /// <summary>Publish immediately after creation (default false = Draft).</summary>
    public bool PublishImmediately { get; set; } = false;
}

public class UpdatePortfolioProjectDto
{
    public string Title { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal? BudgetMin { get; set; }
    public decimal? BudgetMax { get; set; }
    public int? DurationMinDays { get; set; }
    public int? DurationMaxDays { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<string> ScopeOfWork { get; set; } = new();
}

public class UpdatePortfolioStatusDto
{
    public PortfolioStatus Status { get; set; }
    public string? RejectionReason { get; set; }
}

// ── RESPONSE DTOs ───────────────────────────────────────────────────────────

public class PortfolioProjectDto
{
    public Guid Id { get; set; }
    public Guid VendorId { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal? BudgetMin { get; set; }
    public decimal? BudgetMax { get; set; }
    public string BudgetDisplay { get; set; } = string.Empty;
    public int? DurationMinDays { get; set; }
    public int? DurationMaxDays { get; set; }
    public string DurationDisplay { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> ScopeOfWork { get; set; } = new();
    public string Status { get; set; } = string.Empty;
    public string? RejectionReason { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<PortfolioImageDto> BeforeImages { get; set; } = new();
    public List<PortfolioImageDto> AfterImages { get; set; } = new();
    public List<PortfolioImageDto> ReferenceImages { get; set; } = new();

    /// <summary>First "After" image — used as the showcase thumbnail.</summary>
    public string? ThumbnailUrl => AfterImages.FirstOrDefault()?.ImageUrl
                                ?? BeforeImages.FirstOrDefault()?.ImageUrl;
}

public class PortfolioImageDto
{
    public Guid Id { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public int SortOrder { get; set; }
}

public class PortfolioPagedResultDto
{
    public List<PortfolioProjectDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)Math.Max(1, PageSize));
    public bool HasMore => Page < TotalPages;
}
