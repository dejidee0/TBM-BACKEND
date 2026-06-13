using TBM.Core.Entities.Common;
using TBM.Core.Entities.Users;
using TBM.Core.Enums;

namespace TBM.Core.Entities.Subscriptions;

public class Subscription : BaseEntity
{
    public Guid UserId { get; set; }
    public SubscriptionTier Tier { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public BillingCycle BillingCycle { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime? CanceledAt { get; set; }
    public DateTime? PausedAt { get; set; }
    public DateTime? ResumedAt { get; set; }

    public decimal AmountPaid { get; set; }
    public string? PaystackReference { get; set; }
    public string? PaystackSubscriptionCode { get; set; }

    // Quota tracking (resets each billing period)
    public int GenerationsUsed { get; set; } = 0;
    public DateTime QuotaResetAt { get; set; }

    // Applied discount snapshot
    public Guid? DiscountId { get; set; }
    public decimal? DiscountAmount { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public Discount? Discount { get; set; }
}
