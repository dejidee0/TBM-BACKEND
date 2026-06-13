using System.Text.Json;
using Microsoft.Extensions.Logging;
using TBM.Application.DTOs.Subscriptions;
using TBM.Core.Entities.Subscriptions;
using TBM.Core.Enums;
using TBM.Core.Interfaces;

namespace TBM.Application.Services.Subscriptions;

public sealed class PricingConfigService
{
    private readonly IUnitOfWork _uow;
    private readonly DiscountEngine _discountEngine;
    private readonly ILogger<PricingConfigService> _logger;

    public PricingConfigService(
        IUnitOfWork uow,
        DiscountEngine discountEngine,
        ILogger<PricingConfigService> logger)
    {
        _uow = uow;
        _discountEngine = discountEngine;
        _logger = logger;
    }

    public async Task<PricingPageDto> GetAllAsync(string? promoCode = null)
    {
        var configs = await _uow.PricingConfigs.GetAllActiveAsync();
        var dtos = configs.Select(MapToDto).ToList();

        AppliedDiscountDto? appliedDiscount = null;
        if (!string.IsNullOrWhiteSpace(promoCode))
        {
            // Resolve against any tier/cycle to get the discount details for display
            var discount = await _uow.Discounts.GetByCodeAsync(promoCode.Trim());
            if (discount != null && DateTime.UtcNow >= discount.StartsAt && DateTime.UtcNow <= discount.ExpiresAt)
            {
                appliedDiscount = new AppliedDiscountDto
                {
                    Code = discount.Code,
                    Name = discount.Name,
                    Type = discount.Type.ToString(),
                    Value = discount.Value,
                    ApplicableTier = discount.ApplicableTier?.ToString()
                };

                // Apply the discount amount on applicable plan rows
                foreach (var dto in dtos)
                {
                    if (Enum.TryParse<SubscriptionTier>(dto.Tier, out var tier) &&
                        Enum.TryParse<BillingCycle>(dto.BillingCycle, out var cycle))
                    {
                        var resolved = await _discountEngine.ResolveAsync(promoCode, tier, cycle);
                        if (resolved != null)
                        {
                            dto.Price = _discountEngine.ApplyDiscount(dto.Price, resolved);
                        }
                    }
                }
            }
        }

        return new PricingPageDto
        {
            Plans = dtos,
            AppliedDiscount = appliedDiscount
        };
    }

    public async Task<PricingConfigDto?> GetByTierAndCycleAsync(SubscriptionTier tier, BillingCycle cycle)
    {
        var config = await _uow.PricingConfigs.GetByTierAndCycleAsync(tier, cycle);
        return config == null ? null : MapToDto(config);
    }

    public async Task<PricingConfigDto?> UpdateAsync(SubscriptionTier tier, BillingCycle cycle, UpdatePricingConfigDto dto)
    {
        var config = await _uow.PricingConfigs.GetByTierAndCycleAsync(tier, cycle);
        if (config == null)
        {
            return null;
        }

        config.Price = dto.Price;
        config.GenerationsPerMonth = dto.GenerationsPerMonth;
        config.PrioritySupport = dto.PrioritySupport;
        config.UnlimitedGenerations = dto.UnlimitedGenerations;
        config.DisplayName = dto.DisplayName;
        config.Description = dto.Description;
        config.IsActive = dto.IsActive;

        if (dto.Features != null)
        {
            config.Features = JsonSerializer.Serialize(dto.Features);
        }

        config.UpdatedAt = DateTime.UtcNow;

        await _uow.PricingConfigs.UpdateAsync(config);
        await _uow.SaveChangesAsync();

        return MapToDto(config);
    }

    // ─── Seeding ─────────────────────────────────────────────────────────

    public async Task SeedDefaultsAsync()
    {
        var existing = await _uow.PricingConfigs.GetAllActiveAsync();
        if (existing.Count > 0)
        {
            return;
        }

        var defaults = new List<PricingConfig>
        {
            // Economy — free tier, 1 generation
            new PricingConfig
            {
                Tier = SubscriptionTier.Economy,
                BillingCycle = BillingCycle.Monthly,
                Price = 0m,
                Currency = "NGN",
                DisplayName = "Economy",
                GenerationsPerMonth = 1,
                PrioritySupport = false,
                UnlimitedGenerations = false,
                Description = "1 free AI render — no payment required",
                Features = JsonSerializer.Serialize(new[] { "1 AI generation", "Standard quality renders", "Email support" }),
                IsActive = true
            },
            new PricingConfig
            {
                Tier = SubscriptionTier.Economy,
                BillingCycle = BillingCycle.Yearly,
                Price = 0m,
                Currency = "NGN",
                DisplayName = "Economy",
                GenerationsPerMonth = 1,
                PrioritySupport = false,
                UnlimitedGenerations = false,
                Description = "1 free AI render — no payment required",
                Features = JsonSerializer.Serialize(new[] { "1 AI generation", "Standard quality renders", "Email support" }),
                IsActive = true
            },
            // Premium
            new PricingConfig
            {
                Tier = SubscriptionTier.Premium,
                BillingCycle = BillingCycle.Monthly,
                Price = 7500m,
                Currency = "NGN",
                DisplayName = "Premium Monthly",
                GenerationsPerMonth = 20,
                PrioritySupport = false,
                UnlimitedGenerations = false,
                Description = "20 AI renders per month",
                Features = JsonSerializer.Serialize(new[] { "20 AI generations/month", "High quality renders", "Video renders", "Priority email support" }),
                IsActive = true
            },
            new PricingConfig
            {
                Tier = SubscriptionTier.Premium,
                BillingCycle = BillingCycle.Yearly,
                Price = 75000m,
                Currency = "NGN",
                DisplayName = "Premium Yearly",
                GenerationsPerMonth = 20,
                PrioritySupport = false,
                UnlimitedGenerations = false,
                Description = "20 AI renders per month, billed yearly",
                Features = JsonSerializer.Serialize(new[] { "20 AI generations/month", "High quality renders", "Video renders", "Priority email support", "2 months free" }),
                IsActive = true
            },
            // Luxury
            new PricingConfig
            {
                Tier = SubscriptionTier.Luxury,
                BillingCycle = BillingCycle.Monthly,
                Price = 20000m,
                Currency = "NGN",
                DisplayName = "Luxury Monthly",
                GenerationsPerMonth = 9999,
                PrioritySupport = true,
                UnlimitedGenerations = true,
                Description = "Unlimited AI renders with priority support",
                Features = JsonSerializer.Serialize(new[] { "Unlimited AI generations", "4K quality renders", "Video renders", "Priority support", "Early access to new features" }),
                IsActive = true
            },
            new PricingConfig
            {
                Tier = SubscriptionTier.Luxury,
                BillingCycle = BillingCycle.Yearly,
                Price = 200000m,
                Currency = "NGN",
                DisplayName = "Luxury Yearly",
                GenerationsPerMonth = 9999,
                PrioritySupport = true,
                UnlimitedGenerations = true,
                Description = "Unlimited AI renders with priority support, billed yearly",
                Features = JsonSerializer.Serialize(new[] { "Unlimited AI generations", "4K quality renders", "Video renders", "Priority support", "Early access to new features", "2 months free" }),
                IsActive = true
            }
        };

        foreach (var config in defaults)
        {
            await _uow.PricingConfigs.CreateAsync(config);
        }

        await _uow.SaveChangesAsync();
        _logger.LogInformation("Seeded {Count} default pricing configs.", defaults.Count);
    }

    // ─── Private ─────────────────────────────────────────────────────────

    private static PricingConfigDto MapToDto(PricingConfig c)
    {
        List<string> features = new();
        if (!string.IsNullOrWhiteSpace(c.Features))
        {
            try { features = JsonSerializer.Deserialize<List<string>>(c.Features) ?? new(); }
            catch { /* ignore malformed JSON */ }
        }

        return new PricingConfigDto
        {
            Id = c.Id,
            Tier = c.Tier.ToString(),
            BillingCycle = c.BillingCycle.ToString(),
            Price = c.Price,
            Currency = c.Currency,
            DisplayName = c.DisplayName,
            Description = c.Description,
            Features = features,
            GenerationsPerMonth = c.UnlimitedGenerations ? -1 : c.GenerationsPerMonth,
            PrioritySupport = c.PrioritySupport,
            UnlimitedGenerations = c.UnlimitedGenerations
        };
    }
}
