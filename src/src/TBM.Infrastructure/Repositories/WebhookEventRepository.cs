using Microsoft.EntityFrameworkCore;
using TBM.Core.Entities.Payments;
using TBM.Core.Interfaces.Repositories;
using TBM.Infrastructure.Data;

namespace TBM.Infrastructure.Repositories;

public class WebhookEventRepository : IWebhookEventRepository
{
    private readonly ApplicationDbContext _context;

    public WebhookEventRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<WebhookEvent?> GetByReferenceAsync(string reference)
    {
        return await _context.WebhookEvents
            .FirstOrDefaultAsync(x => x.Reference == reference);
    }

    public async Task AddAsync(WebhookEvent webhookEvent)
    {
        await _context.WebhookEvents.AddAsync(webhookEvent);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
