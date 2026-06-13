using TBM.Core.Entities.Common;
using TBM.Core.Enums;

namespace TBM.Core.Entities.Subscriptions;

public class PricingConfig : BaseEntity
{
    public SubscriptionTier Tier { get; set; }
    public BillingCycle BillingCycle { get; set; }

    public decimal Price { get; set; }
    public string Currency { get; set; } = "NGN";

    // Feature limits
    public int GenerationsPerMonth { get; set; }
    public bool PrioritySupport { get; set; } = false;
    public bool UnlimitedGenerations { get; set; } = false;

    // Display
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Features { get; set; } // JSON array of feature strings

    public bool IsActive { get; set; } = true;
}
