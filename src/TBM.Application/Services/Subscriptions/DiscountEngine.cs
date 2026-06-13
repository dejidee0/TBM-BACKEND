using Microsoft.Extensions.Logging;
using TBM.Core.Entities.Subscriptions;
using TBM.Core.Enums;
using TBM.Core.Interfaces;

namespace TBM.Application.Services.Subscriptions;

public sealed class DiscountEngine
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<DiscountEngine> _logger;

    public DiscountEngine(IUnitOfWork uow, ILogger<DiscountEngine> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    /// <summary>
    /// Resolves a promo code against the requested tier/cycle.
    /// Returns null if code is invalid, expired, exhausted, or not applicable.
    /// </summary>
    public async Task<Discount?> ResolveAsync(
        string? promoCode,
        SubscriptionTier tier,
        BillingCycle cycle)
    {
        if (string.IsNullOrWhiteSpace(promoCode))
        {
            return null;
        }

        var discount = await _uow.Discounts.GetByCodeAsync(promoCode.Trim());
        if (discount == null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        if (now < discount.StartsAt || now > discount.ExpiresAt)
        {
            return null;
        }

        if (discount.MaxRedemptions.HasValue && discount.CurrentRedemptions >= discount.MaxRedemptions.Value)
        {
            return null;
        }

        if (discount.ApplicableTier.HasValue && discount.ApplicableTier.Value != tier)
        {
            return null;
        }

        if (discount.ApplicableBillingCycle.HasValue && discount.ApplicableBillingCycle.Value != cycle)
        {
            return null;
        }

        return discount;
    }

    /// <summary>
    /// Calculates the discounted price. Returns original price if discount is null.
    /// </summary>
    public decimal ApplyDiscount(decimal originalPrice, Discount? discount)
    {
        if (discount == null)
        {
            return originalPrice;
        }

        var discounted = discount.Type switch
        {
            DiscountType.Percentage => originalPrice * (1 - discount.Value / 100m),
            DiscountType.FixedAmount => originalPrice - discount.Value,
            _ => originalPrice
        };

        return Math.Max(0m, Math.Round(discounted, 2));
    }

    /// <summary>
    /// Returns only the discount amount (savings), not the final price.
    /// </summary>
    public decimal CalculateDiscountAmount(decimal originalPrice, Discount? discount)
    {
        return originalPrice - ApplyDiscount(originalPrice, discount);
    }

    /// <summary>
    /// Increments the redemption counter for the discount.
    /// Should be called only when a subscription is activated (payment succeeded).
    /// </summary>
    public async Task IncrementRedemptionAsync(Guid discountId)
    {
        var discount = await _uow.Discounts.GetByIdAsync(discountId);
        if (discount == null)
        {
            return;
        }

        discount.CurrentRedemptions++;

        // Auto-deactivate if max reached
        if (discount.MaxRedemptions.HasValue && discount.CurrentRedemptions >= discount.MaxRedemptions.Value)
        {
            discount.IsActive = false;
        }

        await _uow.Discounts.UpdateAsync(discount);
    }
}
