using TBM.Core.Entities.Common;

namespace TBM.Core.Entities;

public class Setting : AuditableEntity
{
    public string Key { get; set; } = null!;
    public string Value { get; set; } = null!;
    public string Category { get; set; } = null!;
    public string? Description { get; set; }
}
