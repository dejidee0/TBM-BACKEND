using TBM.Core.Entities.Common;
using TBM.Core.Enums;

namespace TBM.Core.Entities.Portfolio;

public class PortfolioProjectImage : BaseEntity
{
    public Guid ProjectId { get; set; }
    public VendorPortfolioProject Project { get; set; } = null!;

    public string ImageUrl { get; set; } = string.Empty;
    public PortfolioImageType ImageType { get; set; }
    public string? Caption { get; set; }
    public int SortOrder { get; set; }
}
