using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TBM.Application.Services.DesignFlow;
using TBM.Application.Services.Subscriptions;
using TBM.Core.Entities.Payments;
using TBM.Core.Enums;
using TBM.Core.Interfaces;

namespace TBM.Application.Services;

public class PaystackWebhookService
{
    private const string SubRefPrefix = "sub_";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ProjectService _projectService;
    private readonly SubscriptionService _subscriptionService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaystackWebhookService> _logger;

    public PaystackWebhookService(
        IUnitOfWork unitOfWork,
        ProjectService projectService,
        SubscriptionService subscriptionService,
        IConfiguration configuration,
        ILogger<PaystackWebhookService> logger)
    {
        _unitOfWork = unitOfWork;
        _projectService = projectService;
        _subscriptionService = subscriptionService;
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsValidSignature(string payload, string signature)
    {
        if (string.IsNullOrWhiteSpace(payload) || string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        var secret = GetSecretKey();
        if (string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogWarning("Paystack webhook secret key is missing.");
            return false;
        }

        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var computedSignature = Convert.ToHexString(hash).ToLowerInvariant();
        var incomingSignature = signature.Trim().ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedSignature),
            Encoding.UTF8.GetBytes(incomingSignature));
    }

    public async Task HandleAsync(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return;
        }

        using var json = JsonDocument.Parse(payload);
        var root = json.RootElement;
        var eventType = root.TryGetProperty("event", out var eventElement)
            ? eventElement.GetString()?.Trim()
            : null;

        var reference = root.TryGetProperty("data", out var dataElement) &&
                        dataElement.TryGetProperty("reference", out var referenceElement)
            ? referenceElement.GetString()?.Trim()
            : null;

        if (string.IsNullOrWhiteSpace(eventType) || string.IsNullOrWhiteSpace(reference))
        {
            _logger.LogWarning("Paystack webhook skipped due to missing event or reference.");
            return;
        }

        var existingEvent = await _unitOfWork.WebhookEvents
            .GetByReferenceAndEventTypeAsync(reference, eventType);

        if (existingEvent?.Processed == true)
        {
            return;
        }

        var webhookEvent = existingEvent ?? new WebhookEvent
        {
            Provider = "Paystack",
            EventType = eventType,
            Reference = reference,
            Payload = payload,
            Processed = false,
            CreatedAt = DateTime.UtcNow
        };

        if (existingEvent == null)
        {
            await _unitOfWork.WebhookEvents.AddAsync(webhookEvent);
            await _unitOfWork.SaveChangesAsync();
        }
        else
        {
            webhookEvent.Payload = payload;
        }

        switch (eventType)
        {
            case "charge.success":
                if (reference.StartsWith(SubRefPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    await ActivateSubscriptionAsync(reference, dataElement, webhookEvent);
                }
                else
                {
                    await MarkOrderPaidAsync(reference, webhookEvent);
                }
                break;

            case "charge.failed":
                if (!reference.StartsWith(SubRefPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    await MarkOrderFailedAsync(reference, webhookEvent);
                }
                else
                {
                    webhookEvent.Processed = true;
                    webhookEvent.ProcessedAt = DateTime.UtcNow;
                    await _unitOfWork.SaveChangesAsync();
                }
                break;

            default:
                webhookEvent.Processed = true;
                webhookEvent.ProcessedAt = DateTime.UtcNow;
                await _unitOfWork.SaveChangesAsync();
                break;
        }
    }

    private async Task ActivateSubscriptionAsync(
        string reference,
        JsonElement dataElement,
        WebhookEvent webhookEvent)
    {
        try
        {
            // Extract metadata sent when we initialized the transaction
            if (!dataElement.TryGetProperty("metadata", out var metaElement))
            {
                _logger.LogWarning("Subscription webhook missing metadata for reference {Reference}", reference);
                webhookEvent.Processed = true;
                webhookEvent.ProcessedAt = DateTime.UtcNow;
                await _unitOfWork.SaveChangesAsync();
                return;
            }

            var paymentType = metaElement.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
            var userIdStr = metaElement.TryGetProperty("userId", out var u) ? u.GetString() : null;
            var amountKobo = dataElement.TryGetProperty("amount", out var amountEl) ? amountEl.GetInt32() : 0;
            var amountPaid = amountKobo / 100m;

            if (!Guid.TryParse(userIdStr, out var userId))
            {
                _logger.LogWarning(
                    "Subscription webhook invalid userId in metadata for reference {Reference}: userId={U}",
                    reference, userIdStr);
                webhookEvent.Processed = true;
                webhookEvent.ProcessedAt = DateTime.UtcNow;
                await _unitOfWork.SaveChangesAsync();
                return;
            }

            // Route: renewal extends existing subscription; new subscribe creates one
            if (string.Equals(paymentType, "subscription_renewal", StringComparison.OrdinalIgnoreCase))
            {
                var existingSubIdStr = metaElement.TryGetProperty("existingSubscriptionId", out var eid)
                    ? eid.GetString()
                    : null;

                if (!Guid.TryParse(existingSubIdStr, out var existingSubId))
                {
                    _logger.LogWarning(
                        "Renewal webhook missing existingSubscriptionId for reference {Reference}", reference);
                    webhookEvent.Processed = true;
                    webhookEvent.ProcessedAt = DateTime.UtcNow;
                    await _unitOfWork.SaveChangesAsync();
                    return;
                }

                await _subscriptionService.RenewFromWebhookAsync(reference, existingSubId, amountPaid);
            }
            else
            {
                var tierStr = metaElement.TryGetProperty("tier", out var t) ? t.GetString() : null;
                var cycleStr = metaElement.TryGetProperty("billingCycle", out var c) ? c.GetString() : null;
                var discountIdStr = metaElement.TryGetProperty("discountId", out var d) ? d.GetString() : null;

                if (!Enum.TryParse<SubscriptionTier>(tierStr, true, out var tier) ||
                    !Enum.TryParse<BillingCycle>(cycleStr, true, out var cycle))
                {
                    _logger.LogWarning(
                        "Subscription webhook invalid tier/cycle for reference {Reference}: tier={T} cycle={C}",
                        reference, tierStr, cycleStr);
                    webhookEvent.Processed = true;
                    webhookEvent.ProcessedAt = DateTime.UtcNow;
                    await _unitOfWork.SaveChangesAsync();
                    return;
                }

                Guid? discountId = Guid.TryParse(discountIdStr, out var parsedDiscount) ? parsedDiscount : null;
                await _subscriptionService.ActivateFromWebhookAsync(reference, tier, cycle, userId, amountPaid, discountId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate subscription for reference {Reference}", reference);
        }
        finally
        {
            webhookEvent.Processed = true;
            webhookEvent.ProcessedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
        }
    }

    private async Task MarkOrderPaidAsync(string reference, WebhookEvent webhookEvent)
    {
        var order = await _unitOfWork.Orders.GetByPaymentReferenceAsync(reference)
            ?? await _unitOfWork.Orders.GetByOrderNumberAsync(reference);

        if (order == null)
        {
            _logger.LogWarning("Paystack webhook charge.success could not find order for reference {Reference}", reference);
            webhookEvent.Processed = true;
            webhookEvent.ProcessedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
            return;
        }

        order.PaymentMethod = PaymentMethod.Paystack;
        order.PaymentStatus = PaymentStatus.Paid;
        order.PaymentReference = reference;

        if (order.PaidAt == null)
        {
            order.PaidAt = DateTime.UtcNow;
        }

        if (order.Status == OrderStatus.Pending)
        {
            order.Status = OrderStatus.PaymentReceived;
        }

        webhookEvent.Processed = true;
        webhookEvent.ProcessedAt = DateTime.UtcNow;

        await _unitOfWork.Orders.UpdateAsync(order);
        await _unitOfWork.SaveChangesAsync();

        await _projectService.CreateProjectFromOrderAsync(order.Id);
    }

    private async Task MarkOrderFailedAsync(string reference, WebhookEvent webhookEvent)
    {
        var order = await _unitOfWork.Orders.GetByPaymentReferenceAsync(reference)
            ?? await _unitOfWork.Orders.GetByOrderNumberAsync(reference);

        if (order != null)
        {
            order.PaymentMethod = PaymentMethod.Paystack;
            order.PaymentStatus = PaymentStatus.Failed;
            order.PaymentReference = reference;
            await _unitOfWork.Orders.UpdateAsync(order);
        }

        webhookEvent.Processed = true;
        webhookEvent.ProcessedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync();
    }

    private string? GetSecretKey()
    {
        return _configuration["Paystack:SecretKey"]?.Trim()
            ?? _configuration["Payment:Paystack:SecretKey"]?.Trim();
    }
}
