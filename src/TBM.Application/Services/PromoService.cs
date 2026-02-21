using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using TBM.Application.DTOs.Checkout;
using TBM.Application.Interfaces;

namespace TBM.Application.Services;

public class PromoService : IPromoService
{
    private static readonly ConcurrentDictionary<string, PromoAttemptState> AttemptStates = new();
    private static readonly Dictionary<string, PromoDefinition> PromoCatalog =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["WELCOME10"] = new PromoDefinition("WELCOME10", "percentage", 10m, minimumOrder: 10000m),
            ["FLOOR15"] = new PromoDefinition("FLOOR15", "percentage", 15m, minimumOrder: 25000m, maxDiscount: 50000m),
            ["SHIPFREE"] = new PromoDefinition("SHIPFREE", "fixed", 5000m, minimumOrder: 50000m)
        };

    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan BlockDuration = TimeSpan.FromMinutes(15);
    private readonly AuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PromoService(AuditService auditService, IHttpContextAccessor httpContextAccessor)
    {
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<PromoValidationResultDto> ValidateAsync(Guid userId, decimal subTotal, string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Failed("Promo code is required.");
        }

        var normalizedCode = code.Trim().ToUpperInvariant();
        var attemptKey = BuildAttemptKey(userId, normalizedCode);

        if (IsBlocked(attemptKey, out var blockedUntil))
        {
            var blockedMessage = $"Too many failed attempts. Try again after {blockedUntil:O}.";
            await LogAsync("Promo.Validation.Blocked", userId, normalizedCode, blockedMessage);
            return Failed(blockedMessage);
        }

        if (!PromoCatalog.TryGetValue(normalizedCode, out var promo) || !promo.IsActive)
        {
            await HandleFailedAttemptAsync(attemptKey, userId, normalizedCode, "Promo code does not exist.");
            return Failed("Invalid promo code.");
        }

        if (promo.ExpiresAtUtc.HasValue && promo.ExpiresAtUtc.Value <= DateTime.UtcNow)
        {
            await HandleFailedAttemptAsync(attemptKey, userId, normalizedCode, "Promo code has expired.");
            return Failed("Promo code has expired.");
        }

        if (subTotal < promo.MinimumOrder)
        {
            var message = $"Minimum order of {promo.MinimumOrder:N2} is required for this promo.";
            await HandleFailedAttemptAsync(attemptKey, userId, normalizedCode, message);
            return Failed(message);
        }

        var discountAmount = CalculateDiscount(subTotal, promo);
        ResetAttempts(attemptKey);

        var successResult = new PromoValidationResultDto
        {
            Success = true,
            Code = promo.Code,
            Discount = promo.Value,
            Type = promo.Type,
            DiscountAmount = discountAmount,
            Message = "Promo code applied successfully."
        };

        await _auditService.LogAsync(
            action: "Promo.Validation.Success",
            category: "Commerce",
            oldValue: null,
            newValue: new
            {
                userId,
                promo = promo.Code,
                subTotal,
                discountAmount
            });

        return successResult;
    }

    private static decimal CalculateDiscount(decimal subTotal, PromoDefinition promo)
    {
        decimal amount;

        if (promo.Type.Equals("percentage", StringComparison.OrdinalIgnoreCase))
        {
            amount = Math.Round(subTotal * (promo.Value / 100m), 2, MidpointRounding.AwayFromZero);
            if (promo.MaxDiscount.HasValue && amount > promo.MaxDiscount.Value)
            {
                amount = promo.MaxDiscount.Value;
            }
        }
        else
        {
            amount = promo.Value;
        }

        return amount > subTotal ? subTotal : amount;
    }

    private async Task HandleFailedAttemptAsync(string attemptKey, Guid userId, string promoCode, string reason)
    {
        RegisterFailedAttempt(attemptKey);
        await LogAsync("Promo.Validation.Failed", userId, promoCode, reason);
    }

    private async Task LogAsync(string action, Guid userId, string promoCode, string reason)
    {
        var ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

        await _auditService.LogAsync(
            action: action,
            category: "Commerce",
            oldValue: null,
            newValue: new
            {
                userId,
                promoCode,
                reason,
                ip
            });
    }

    private bool IsBlocked(string attemptKey, out DateTime blockedUntil)
    {
        blockedUntil = DateTime.MinValue;

        if (!AttemptStates.TryGetValue(attemptKey, out var state))
        {
            return false;
        }

        lock (state)
        {
            if (state.BlockedUntilUtc.HasValue && state.BlockedUntilUtc.Value > DateTime.UtcNow)
            {
                blockedUntil = state.BlockedUntilUtc.Value;
                return true;
            }

            if (state.BlockedUntilUtc.HasValue && state.BlockedUntilUtc.Value <= DateTime.UtcNow)
            {
                state.BlockedUntilUtc = null;
                state.FailedCount = 0;
                state.WindowStartedAtUtc = DateTime.UtcNow;
            }
        }

        return false;
    }

    private void RegisterFailedAttempt(string attemptKey)
    {
        var state = AttemptStates.GetOrAdd(attemptKey, _ => new PromoAttemptState
        {
            FailedCount = 0,
            WindowStartedAtUtc = DateTime.UtcNow
        });

        lock (state)
        {
            var now = DateTime.UtcNow;

            if (now - state.WindowStartedAtUtc > AttemptWindow)
            {
                state.FailedCount = 0;
                state.WindowStartedAtUtc = now;
                state.BlockedUntilUtc = null;
            }

            state.FailedCount++;

            if (state.FailedCount >= MaxFailedAttempts)
            {
                state.BlockedUntilUtc = now.Add(BlockDuration);
            }
        }
    }

    private static void ResetAttempts(string attemptKey)
    {
        AttemptStates.TryRemove(attemptKey, out _);
    }

    private string BuildAttemptKey(Guid userId, string code)
    {
        var ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"{userId:N}:{ip}:{code}";
    }

    private static PromoValidationResultDto Failed(string message)
    {
        return new PromoValidationResultDto
        {
            Success = false,
            Message = message,
            Type = string.Empty
        };
    }

    private sealed class PromoAttemptState
    {
        public int FailedCount { get; set; }
        public DateTime WindowStartedAtUtc { get; set; }
        public DateTime? BlockedUntilUtc { get; set; }
    }

    private sealed class PromoDefinition
    {
        public PromoDefinition(
            string code,
            string type,
            decimal value,
            decimal minimumOrder,
            decimal? maxDiscount = null,
            DateTime? expiresAtUtc = null,
            bool isActive = true)
        {
            Code = code;
            Type = type;
            Value = value;
            MinimumOrder = minimumOrder;
            MaxDiscount = maxDiscount;
            ExpiresAtUtc = expiresAtUtc;
            IsActive = isActive;
        }

        public string Code { get; }
        public string Type { get; }
        public decimal Value { get; }
        public decimal MinimumOrder { get; }
        public decimal? MaxDiscount { get; }
        public DateTime? ExpiresAtUtc { get; }
        public bool IsActive { get; }
    }
}
