using TBM.Core.Entities.Payments;

namespace TBM.Core.Interfaces.Repositories;

public interface IWebhookEventRepository
{
    Task<WebhookEvent?> GetByReferenceAsync(string reference);
    Task AddAsync(WebhookEvent webhookEvent);
    Task SaveChangesAsync();
}
