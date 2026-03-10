using TBM.Application.DTOs.AI;

namespace TBM.Application.Interfaces;

public interface IAssistantLlmClient
{
    Task<AssistantLlmResponseDto?> GenerateAsync(AssistantLlmRequestDto request, CancellationToken cancellationToken = default);
}
