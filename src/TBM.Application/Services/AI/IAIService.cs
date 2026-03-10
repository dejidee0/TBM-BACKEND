using TBM.Application.DTOs.AI;
using TBM.Core.Entities.AI;

namespace TBM.Application.Interfaces;

public interface IAIService
{
    Task<AIProject> CreateProjectAsync(Guid userId, CreateAIProjectDto dto);
    Task<List<AIProjectListItemDto>> GetUserProjectsAsync(Guid userId);
    Task<AIDesign> GenerateImageAsync(Guid userId, GenerateImageDto dto);
    Task<AIDesign> GenerateVideoAsync(Guid userId, GenerateVideoDto dto);
}
