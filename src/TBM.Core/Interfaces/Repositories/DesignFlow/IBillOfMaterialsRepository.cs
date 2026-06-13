using TBM.Core.Entities.DesignFlow;

namespace TBM.Core.Interfaces.Repositories.DesignFlow;

public interface IBillOfMaterialsRepository
{
    Task<BillOfMaterials> CreateAsync(BillOfMaterials bom);
    Task<BillOfMaterials?> GetByIdAsync(Guid id);
    Task<BillOfMaterials?> GetByDesignSessionIdAsync(Guid designSessionId);
    Task UpdateAsync(BillOfMaterials bom);
    Task<string> GenerateBomNumberAsync();
}
