using TBM.Core.Entities.Payments;

namespace TBM.Core.Interfaces.Repositories;

public interface IWebhookEventRepository
{
    Task<WebhookEvent?> GetByReferenceAsync(string reference);
    Task<WebhookEvent?> GetByReferenceAndEventTypeAsync(string reference, string eventType);
    Task<WebhookEvent?> GetLatestByReferenceAsync(string reference);
    Task AddAsync(WebhookEvent webhookEvent);
    Task SaveChangesAsync();
}
