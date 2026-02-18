using TBM.Core.Models.AI;

namespace TBM.Core.Interfaces.AI
{
    public interface IAIProvider
    {
        string ProviderName { get; }

        Task<AIProviderResult> GenerateImageAsync(AIImageRequest request);

        Task<AIProviderResult> GenerateVideoAsync(AIVideoRequest request);
    }
}
