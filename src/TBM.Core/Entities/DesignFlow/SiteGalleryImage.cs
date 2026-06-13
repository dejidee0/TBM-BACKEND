using TBM.Core.Entities.Common;

namespace TBM.Core.Entities.DesignFlow;

public class SiteGalleryImage : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string Thumbnail { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public Guid UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; }
    public int SortOrder { get; set; }
}
