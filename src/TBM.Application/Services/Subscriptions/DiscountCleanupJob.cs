using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TBM.Core.Interfaces;

namespace TBM.Application.Services.Subscriptions;

/// <summary>
/// Hourly background job that deactivates expired discount/promo codes.
/// </summary>
public sealed class DiscountCleanupJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DiscountCleanupJob> _logger;

    public DiscountCleanupJob(
        IServiceScopeFactory scopeFactory,
        ILogger<DiscountCleanupJob> logger)
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

                await DeactivateExpiredDiscountsAsync(uow);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DiscountCleanupJob encountered an error.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task DeactivateExpiredDiscountsAsync(IUnitOfWork uow)
    {
        var now = DateTime.UtcNow;
        var expired = await uow.Discounts.GetExpiredAsync(now);

        if (expired.Count == 0)
        {
            return;
        }

        foreach (var discount in expired)
        {
            discount.IsActive = false;
            await uow.Discounts.UpdateAsync(discount);
        }

        await uow.SaveChangesAsync();
        _logger.LogInformation("Deactivated {Count} expired discount(s).", expired.Count);
    }
}
