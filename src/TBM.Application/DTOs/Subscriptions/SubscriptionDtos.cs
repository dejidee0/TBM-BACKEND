using TBM.Core.Enums;

namespace TBM.Application.DTOs.Subscriptions;

// ─── Inbound ────────────────────────────────────────────────

public record SubscribeToPlanDto(
    SubscriptionTier Tier,
    BillingCycle Cycle,
    string? PromoCode,
    string? CallbackUrl);

public sealed class ActivateSubscriptionDto
{
    public string Reference { get; set; } = string.Empty;
    public SubscriptionTier Tier { get; set; }
    public BillingCycle Cycle { get; set; }
    public string? PromoCode { get; set; }
}

// ─── Outbound ───────────────────────────────────────────────

public sealed class CurrentSubscriptionDto
{
    public Guid Id { get; set; }
    public string Tier { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string BillingCycle { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int GenerationsUsed { get; set; }
    public int GenerationsLimit { get; set; }
    public bool UnlimitedGenerations { get; set; }
    public bool PrioritySupport { get; set; }
    public decimal AmountPaid { get; set; }
    public DateTime QuotaResetAt { get; set; }
}

public sealed class SubscribeInitDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string? AuthorizationUrl { get; set; }
    public string? AccessCode { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal FinalPrice { get; set; }
    public decimal? DiscountAmount { get; set; }
    public string? DiscountCode { get; set; }
}

public sealed class PricingConfigDto
{
    public Guid Id { get; set; }
    public string Tier { get; set; } = string.Empty;
    public string BillingCycle { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "NGN";
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> Features { get; set; } = new();
    public int GenerationsPerMonth { get; set; }
    public bool PrioritySupport { get; set; }
    public bool UnlimitedGenerations { get; set; }
}

public sealed class PricingPageDto
{
    public List<PricingConfigDto> Plans { get; set; } = new();
    public AppliedDiscountDto? AppliedDiscount { get; set; }
}

public sealed class AppliedDiscountDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string? ApplicableTier { get; set; }
}

// ─── Admin Inbound ──────────────────────────────────────────

public sealed class UpdatePricingConfigDto
{
    public decimal Price { get; set; }
    public int GenerationsPerMonth { get; set; }
    public bool PrioritySupport { get; set; }
    public bool UnlimitedGenerations { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string>? Features { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class CreateDiscountDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Type { get; set; } = "Percentage"; // "Percentage" | "FixedAmount"
    public decimal Value { get; set; }
    public string? ApplicableTier { get; set; }
    public string? ApplicableBillingCycle { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int? MaxRedemptions { get; set; }
    public bool SingleUsePerUser { get; set; } = false;
}

public sealed class UpdateDiscountDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Value { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int? MaxRedemptions { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class DiscountDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string? ApplicableTier { get; set; }
    public string? ApplicableBillingCycle { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsActive { get; set; }
    public int CurrentRedemptions { get; set; }
    public int? MaxRedemptions { get; set; }
    public DateTime CreatedAt { get; set; }
}
