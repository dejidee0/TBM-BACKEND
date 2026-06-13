using TBM.Core.Entities.AI;

namespace TBM.Core.Interfaces.Repositories.AI;

public interface IAIAssistantRepository
{
    Task CreateSessionAsync(AIAssistantSession session);
    Task<AIAssistantSession?> GetSessionAsync(Guid sessionId, Guid userId);
    Task<List<AIAssistantSession>> GetSessionsAsync(Guid userId, int take = 20);
    Task AddMessageAsync(AIAssistantMessage message);
    Task AddTaskAsync(AIAssistantTask task);
    Task AddToolActionAsync(AIAssistantToolAction toolAction);
    Task AddToolApprovalAsync(AIAssistantToolApproval approval);
    Task AddToolExecutionAsync(AIAssistantToolExecution execution);
    Task<AIAssistantTask?> GetTaskAsync(Guid taskId, Guid userId);
    Task<AIAssistantToolAction?> GetToolActionAsync(Guid actionId, Guid userId);
    Task<AIAssistantToolApproval?> GetLatestApprovalAsync(Guid actionId, Guid userId);
    Task<bool> DeleteSessionAsync(Guid sessionId, Guid userId);
}
