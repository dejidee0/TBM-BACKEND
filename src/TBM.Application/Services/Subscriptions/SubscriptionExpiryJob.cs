using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TBM.Core.Enums;
using TBM.Core.Interfaces;

namespace TBM.Application.Services.Subscriptions;

/// <summary>
/// Daily background job that marks expired subscriptions and resets monthly generation quotas.
/// </summary>
public sealed class SubscriptionExpiryJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SubscriptionExpiryJob> _logger;

    public SubscriptionExpiryJob(
        IServiceScopeFactory scopeFactory,
        ILogger<SubscriptionExpiryJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                await ExpireSubscriptionsAsync(uow);
                await ResetMonthlyQuotasAsync(uow);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SubscriptionExpiryJob encountered an error.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ExpireSubscriptionsAsync(IUnitOfWork uow)
    {
        var now = DateTime.UtcNow;
        var expiring = await uow.Subscriptions.GetExpiringBeforeAsync(now);

        if (expiring.Count == 0)
        {
            return;
        }

        foreach (var sub in expiring)
        {
            sub.Status = SubscriptionStatus.Expired;
            await uow.Subscriptions.UpdateAsync(sub);
        }

        await uow.SaveChangesAsync();
        _logger.LogInformation("Expired {Count} subscription(s).", expiring.Count);
    }

    private async Task ResetMonthlyQuotasAsync(IUnitOfWork uow)
    {
        // Reset generation counts for subscriptions whose QuotaResetAt has passed
        // (The EndDate doubles as QuotaResetAt for the current period)
        // This handles mid-period resets for yearly plans that reset monthly
        // For now: quota resets on renewal, so this is a no-op placeholder.
        // Full monthly reset for yearly plans can be added here if needed.
        await Task.CompletedTask;
    }
}
