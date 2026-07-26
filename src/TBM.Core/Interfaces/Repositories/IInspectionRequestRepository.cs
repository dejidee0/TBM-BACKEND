using TBM.Core.Entities.Inspections;

namespace TBM.Core.Interfaces.Repositories;

public interface IInspectionRequestRepository
{
    Task<InspectionRequest> CreateAsync(InspectionRequest request);
    Task<InspectionRequest?> GetByIdAsync(Guid id);
    Task<InspectionRequest?> GetByPaymentReferenceAsync(string paymentReference);
    Task UpdateAsync(InspectionRequest request);
}
