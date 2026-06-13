using TBM.Core.Entities.Common;
using TBM.Core.Enums;

namespace TBM.Core.Entities.Subscriptions;

public class Discount : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public DiscountType Type { get; set; }
    public decimal Value { get; set; } // percentage (0-100) or fixed NGN amount

    // Applicability
    public SubscriptionTier? ApplicableTier { get; set; } // null = all tiers
    public BillingCycle? ApplicableBillingCycle { get; set; } // null = all cycles

    // Validity
    public DateTime StartsAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Usage limits
    public int? MaxRedemptions { get; set; } // null = unlimited
    public int CurrentRedemptions { get; set; } = 0;
    public bool SingleUsePerUser { get; set; } = false;

    // Navigation
    public List<Subscription> Subscriptions { get; set; } = new();
}
