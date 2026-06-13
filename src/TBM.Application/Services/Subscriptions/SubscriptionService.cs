using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TBM.Application.Configuration;
using TBM.Application.DTOs.Subscriptions;
using TBM.Core.Entities.Subscriptions;
using TBM.Core.Enums;
using TBM.Core.Interfaces;

namespace TBM.Application.Services.Subscriptions;

public sealed class SubscriptionService
{
    private const string SubRefPrefix = "sub_";

    private readonly IUnitOfWork _uow;
    private readonly PaystackService _paystack;
    private readonly DiscountEngine _discountEngine;
    private readonly ILogger<SubscriptionService> _logger;
    private readonly AppSettings _appSettings;

    public SubscriptionService(
        IUnitOfWork uow,
        PaystackService paystack,
        DiscountEngine discountEngine,
        ILogger<SubscriptionService> logger,
        IOptions<AppSettings> appSettings)
    {
        _uow = uow;
        _paystack = paystack;
        _discountEngine = discountEngine;
        _logger = logger;
        _appSettings = appSettings.Value;
    }

    // ─── Public ──────────────────────────────────────────────────────────

    public async Task<CurrentSubscriptionDto?> GetCurrentAsync(Guid userId)
    {
        var sub = await _uow.Subscriptions.GetActiveByUserIdAsync(userId);
        if (sub == null)
        {
            return null;
        }

        var pricing = await _uow.PricingConfigs.GetByTierAndCycleAsync(sub.Tier, sub.BillingCycle);

        return new CurrentSubscriptionDto
        {
            Id = sub.Id,
            Tier = sub.Tier.ToString(),
            Status = sub.Status.ToString(),
            BillingCycle = sub.BillingCycle.ToString(),
            StartDate = sub.StartDate,
            EndDate = sub.EndDate,
            GenerationsUsed = sub.GenerationsUsed,
            GenerationsLimit = pricing?.GenerationsPerMonth ?? 0,
            UnlimitedGenerations = pricing?.UnlimitedGenerations ?? false,
            PrioritySupport = pricing?.PrioritySupport ?? false,
            AmountPaid = sub.AmountPaid,
            QuotaResetAt = sub.QuotaResetAt
        };
    }

    public async Task<SubscribeInitDto> SubscribeAsync(Guid userId, SubscribeToPlanDto dto)
    {
        var user = await _uow.Users.GetByIdAsync(userId);
        if (user == null)
        {
            return Fail("User not found.");
        }

        var existing = await _uow.Subscriptions.GetActiveByUserIdAsync(userId);
        if (existing != null)
        {
            return Fail("You already have an active subscription. Cancel it before subscribing to a new plan.");
        }

        var pricing = await _uow.PricingConfigs.GetByTierAndCycleAsync(dto.Tier, dto.Cycle);
        if (pricing == null)
        {
            return Fail("Selected plan is not available.");
        }

        var discount = await _discountEngine.ResolveAsync(dto.PromoCode, dto.Tier, dto.Cycle);
        var finalPrice = _discountEngine.ApplyDiscount(pricing.Price, discount);
        var discountAmount = _discountEngine.CalculateDiscountAmount(pricing.Price, discount);

        // Free tier — activate immediately without going to Paystack
        if (finalPrice == 0m)
        {
            await ActivateFromWebhookAsync(
                paystackReference: BuildReference(userId),
                tier: dto.Tier,
                cycle: dto.Cycle,
                userId: userId,
                amountPaid: 0m,
                discountId: null);

            return new SubscribeInitDto
            {
                Success = true,
                Message = "Free plan activated successfully.",
                Reference = string.Empty,
                AuthorizationUrl = null,
                AccessCode = null,
                OriginalPrice = 0m,
                FinalPrice = 0m,
                DiscountAmount = null,
                DiscountCode = null
            };
        }

        var reference = BuildReference(userId);
        var metadata = new
        {
            type = "subscription",
            userId = userId.ToString(),
            tier = dto.Tier.ToString(),
            billingCycle = dto.Cycle.ToString(),
            pricingConfigId = pricing.Id.ToString(),
            discountId = discount?.Id.ToString(),
            discountCode = discount?.Code
        };

        var result = await _paystack.InitializeTransactionAsync(new PaystackInitializeRequest
        {
            Email = user.Email,
            Amount = finalPrice,
            Reference = reference,
            CallbackUrl = dto.CallbackUrl,
            Metadata = metadata
        });

        if (!result.Success)
        {
            return Fail(result.Message);
        }

        return new SubscribeInitDto
        {
            Success = true,
            Message = "Payment initialized. Complete payment to activate subscription.",
            Reference = result.Reference,
            AuthorizationUrl = result.AuthorizationUrl,
            AccessCode = result.AccessCode,
            OriginalPrice = pricing.Price,
            FinalPrice = finalPrice,
            DiscountAmount = discount != null ? discountAmount : null,
            DiscountCode = discount?.Code
        };
    }

    /// <summary>
    /// Called from the Paystack webhook (charge.success) after payment verification.
    /// </summary>
    public async Task ActivateFromWebhookAsync(
        string paystackReference,
        SubscriptionTier tier,
        BillingCycle cycle,
        Guid userId,
        decimal amountPaid,
        Guid? discountId)
    {
        var existingActive = await _uow.Subscriptions.GetActiveByUserIdAsync(userId);
        if (existingActive != null)
        {
            _logger.LogWarning(
                "Subscription webhook skipped — user {UserId} already has active subscription {SubId}",
                userId, existingActive.Id);
            return;
        }

        var pricing = await _uow.PricingConfigs.GetByTierAndCycleAsync(tier, cycle);
        var now = DateTime.UtcNow;

        decimal? discountAmount = null;
        if (discountId.HasValue && pricing != null)
        {
            var discount = await _uow.Discounts.GetByIdAsync(discountId.Value);
            if (discount != null)
            {
                discountAmount = _discountEngine.CalculateDiscountAmount(pricing.Price, discount);
            }
        }

        var endDate = cycle == BillingCycle.Monthly
            ? now.AddMonths(1)
            : now.AddYears(1);

        var subscription = new Subscription
        {
            UserId = userId,
            Tier = tier,
            Status = SubscriptionStatus.Active,
            BillingCycle = cycle,
            StartDate = now,
            EndDate = endDate,
            AmountPaid = amountPaid,
            PaystackReference = paystackReference,
            GenerationsUsed = 0,
            QuotaResetAt = endDate,
            DiscountId = discountId,
            DiscountAmount = discountAmount
        };

        await _uow.Subscriptions.CreateAsync(subscription);
        await _uow.SaveChangesAsync();

        if (discountId.HasValue)
        {
            await _discountEngine.IncrementRedemptionAsync(discountId.Value);
            await _uow.SaveChangesAsync();
        }

        _logger.LogInformation(
            "Subscription activated for user {UserId}: tier={Tier} cycle={Cycle} ref={Ref}",
            userId, tier, cycle, paystackReference);
    }

    /// <summary>
    /// Called from the Paystack webhook for a renewal payment (charge.success with type=subscription_renewal).
    /// Extends the existing subscription's period and resets the generation quota.
    /// </summary>
    public async Task RenewFromWebhookAsync(
        string paystackReference,
        Guid existingSubscriptionId,
        decimal amountPaid)
    {
        var sub = await _uow.Subscriptions.GetByIdAsync(existingSubscriptionId);
        if (sub == null)
        {
            _logger.LogWarning(
                "Renewal webhook could not find subscription {SubId} for reference {Ref}",
                existingSubscriptionId, paystackReference);
            return;
        }

        var now = DateTime.UtcNow;
        var extensionBase = sub.EndDate > now ? sub.EndDate : now;

        sub.EndDate = sub.BillingCycle == BillingCycle.Monthly
            ? extensionBase.AddMonths(1)
            : extensionBase.AddYears(1);

        sub.Status = SubscriptionStatus.Active;
        sub.AmountPaid += amountPaid;
        sub.PaystackReference = paystackReference;
        sub.GenerationsUsed = 0;
        sub.QuotaResetAt = sub.EndDate;

        await _uow.Subscriptions.UpdateAsync(sub);
        await _uow.SaveChangesAsync();

        _logger.LogInformation(
            "Subscription renewed for user {UserId}: subId={SubId} newEndDate={End} ref={Ref}",
            sub.UserId, sub.Id, sub.EndDate, paystackReference);
    }

    public async Task<bool> CancelAsync(Guid userId)
    {
        var sub = await _uow.Subscriptions.GetActiveByUserIdAsync(userId);
        if (sub == null)
        {
            return false;
        }

        sub.Status = SubscriptionStatus.Canceled;
        sub.CanceledAt = DateTime.UtcNow;
        await _uow.Subscriptions.UpdateAsync(sub);
        await _uow.SaveChangesAsync();
        return true;
    }

    public async Task<SubscribeInitDto> RenewAsync(Guid userId, string? callbackUrl = null)
    {
        var user = await _uow.Users.GetByIdAsync(userId);
        if (user == null)
        {
            return Fail("User not found.");
        }

        var sub = await _uow.Subscriptions.GetActiveByUserIdAsync(userId);
        if (sub == null)
        {
            return Fail("No active subscription to renew.");
        }

        var pricing = await _uow.PricingConfigs.GetByTierAndCycleAsync(sub.Tier, sub.BillingCycle);
        if (pricing == null)
        {
            return Fail("Current plan pricing is no longer available.");
        }

        var reference = BuildReference(userId);
        var metadata = new
        {
            type = "subscription_renewal",
            userId = userId.ToString(),
            tier = sub.Tier.ToString(),
            billingCycle = sub.BillingCycle.ToString(),
            existingSubscriptionId = sub.Id.ToString()
        };

        var result = await _paystack.InitializeTransactionAsync(new PaystackInitializeRequest
        {
            Email = user.Email,
            Amount = pricing.Price,
            Reference = reference,
            CallbackUrl = callbackUrl,
            Metadata = metadata
        });

        if (!result.Success)
        {
            return Fail(result.Message);
        }

        return new SubscribeInitDto
        {
            Success = true,
            Message = "Renewal payment initialized.",
            Reference = result.Reference,
            AuthorizationUrl = result.AuthorizationUrl,
            AccessCode = result.AccessCode,
            OriginalPrice = pricing.Price,
            FinalPrice = pricing.Price
        };
    }

    /// <summary>
    /// Called from the frontend callback after Paystack redirects back.
    /// Verifies payment with Paystack and activates subscription if not already done by webhook.
    /// </summary>
    public async Task<(bool Success, string Message, CurrentSubscriptionDto? Subscription)> ActivateByReferenceAsync(
        Guid userId,
        string reference,
        SubscriptionTier tier,
        BillingCycle cycle,
        string? promoCode = null)
    {
        // Free tier: no Paystack reference needed — activate immediately via the same
        // path used by SubscribeAsync when finalPrice == 0.
        if (tier == SubscriptionTier.Economy && string.IsNullOrWhiteSpace(reference))
        {
            var existingFree = await _uow.Subscriptions.GetActiveByUserIdAsync(userId);
            if (existingFree != null)
            {
                var current = await GetCurrentAsync(userId);
                return (true, "Subscription already active.", current);
            }

            var discount = await _discountEngine.ResolveAsync(promoCode, tier, cycle);
            await ActivateFromWebhookAsync(BuildReference(userId), tier, cycle, userId, 0m, discount?.Id);
            var freeSub = await GetCurrentAsync(userId);
            return (true, "Free plan activated successfully.", freeSub);
        }

        if (!reference.StartsWith(SubRefPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Invalid subscription reference. Call /subscribe first to obtain a payment reference.", null);
        }

        // Idempotency: if already activated by webhook, return current sub
        var existing = await _uow.Subscriptions.GetActiveByUserIdAsync(userId);
        if (existing?.PaystackReference == reference)
        {
            var dto = await GetCurrentAsync(userId);
            return (true, "Subscription already active.", dto);
        }

        // Verify payment with Paystack
        var verified = await _paystack.VerifyTransactionAsync(reference);
        if (!verified.Success || !string.Equals(verified.Status, "success", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Payment could not be verified. Please contact support if payment was deducted.", null);
        }

        // Determine if this is a renewal (existing active sub with different reference)
        if (existing != null)
        {
            // Treat as renewal — extend the current subscription
            await RenewFromWebhookAsync(reference, existing.Id, verified.Amount);
        }
        else
        {
            var discount = await _discountEngine.ResolveAsync(promoCode, tier, cycle);
            await ActivateFromWebhookAsync(reference, tier, cycle, userId, verified.Amount, discount?.Id);
        }

        var sub = await GetCurrentAsync(userId);
        return (true, "Subscription activated successfully.", sub);
    }

    /// <summary>
    /// Upgrades to a new tier: cancels current subscription and initiates payment for the new one.
    /// </summary>
    public async Task<SubscribeInitDto> UpgradeAsync(Guid userId, SubscribeToPlanDto dto)
    {
        var current = await _uow.Subscriptions.GetActiveByUserIdAsync(userId);
        if (current != null)
        {
            // Cancel existing subscription so SubscribeAsync doesn't block
            current.Status = SubscriptionStatus.Canceled;
            current.CanceledAt = DateTime.UtcNow;
            await _uow.Subscriptions.UpdateAsync(current);
            await _uow.SaveChangesAsync();
        }

        return await SubscribeAsync(userId, dto);
    }

    // ─── Quota Enforcement ───────────────────────────────────────────────

    /// <summary>
    /// Verifies the user has an active subscription with quota remaining.
    /// Throws <see cref="SubscriptionQuotaException"/> if blocked.
    /// </summary>
    public async Task CheckAndIncrementQuotaAsync(Guid userId)
    {
        var sub = await _uow.Subscriptions.GetActiveByUserIdAsync(userId);

        if (sub == null)
        {
            throw new SubscriptionQuotaException(
                "SUBSCRIPTION_REQUIRED",
                $"An active subscription is required to generate AI designs. Visit {_appSettings.FrontendBaseUrl}/pricing to subscribe.");
        }

        if (sub.Status != SubscriptionStatus.Active)
        {
            throw new SubscriptionQuotaException(
                "SUBSCRIPTION_INACTIVE",
                $"Your subscription is {sub.Status.ToString().ToLower()}. Renew to continue generating AI designs.");
        }

        if (sub.EndDate <= DateTime.UtcNow)
        {
            sub.Status = SubscriptionStatus.Expired;
            await _uow.Subscriptions.UpdateAsync(sub);
            await _uow.SaveChangesAsync();

            throw new SubscriptionQuotaException(
                "SUBSCRIPTION_EXPIRED",
                "Your subscription has expired. Renew to continue generating AI designs.");
        }

        var pricing = await _uow.PricingConfigs.GetByTierAndCycleAsync(sub.Tier, sub.BillingCycle);
        if (pricing == null || pricing.UnlimitedGenerations)
        {
            // Luxury / unlimited — still track usage but don't block
            sub.GenerationsUsed++;
            await _uow.Subscriptions.UpdateAsync(sub);
            await _uow.SaveChangesAsync();
            return;
        }

        if (sub.GenerationsUsed >= pricing.GenerationsPerMonth)
        {
            throw new SubscriptionQuotaException(
                "QUOTA_EXCEEDED",
                $"You have used all {pricing.GenerationsPerMonth} AI generations for this billing period. " +
                "Upgrade your plan or wait until your quota resets.");
        }

        sub.GenerationsUsed++;
        await _uow.Subscriptions.UpdateAsync(sub);
        await _uow.SaveChangesAsync();
    }

    // ─── Private ─────────────────────────────────────────────────────────

    private static string BuildReference(Guid userId)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        return $"{SubRefPrefix}{userId:N}_{DateTime.UtcNow:yyyyMMddHHmmss}_{suffix}";
    }

    private static SubscribeInitDto Fail(string message) => new()
    {
        Success = false,
        Message = message
    };
}
