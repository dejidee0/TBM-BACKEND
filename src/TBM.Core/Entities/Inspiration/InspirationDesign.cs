using TBM.Core.Entities.Common;

namespace TBM.Core.Entities.Inspiration;

public class InspirationDesign : AuditableEntity
{
    public string Title { get; set; } = string.Empty;
    public string Style { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}
