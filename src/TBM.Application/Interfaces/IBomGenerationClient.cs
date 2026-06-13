using TBM.Application.DTOs.DesignFlow;

namespace TBM.Application.Interfaces;

public interface IBomGenerationClient
{
    Task<BomGenerationResultDto?> GenerateAsync(BomGenerationRequestDto request, CancellationToken cancellationToken = default);
}
