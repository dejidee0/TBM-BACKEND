using TBM.Core.Entities.Common;
using TBM.Core.Enums;

namespace TBM.Core.Entities.DesignFlow;

public class BillOfMaterials : AuditableEntity
{
    public Guid DesignSessionId { get; set; }
    public string BomNumber { get; set; } = string.Empty;
    public decimal TotalEstimatedCost { get; set; }
    public int ItemCount { get; set; }
    public BillOfMaterialsStatus Status { get; set; } = BillOfMaterialsStatus.Draft;

    public ICollection<BOMItem> Items { get; set; } = new List<BOMItem>();
}
