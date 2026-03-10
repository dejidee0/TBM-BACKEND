using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TBM.Core.Entities.Payments;
using TBM.Core.Enums;
using TBM.Core.Interfaces;

namespace TBM.Application.Services;

public class PaystackWebhookService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaystackWebhookService> _logger;

    public PaystackWebhookService(
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        ILogger<PaystackWebhookService> logger)
    {
        _unitOfWork = unitOfWork;
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
                await MarkOrderPaidAsync(reference, webhookEvent);
                break;

            case "charge.failed":
                await MarkOrderFailedAsync(reference, webhookEvent);
                break;

            default:
                webhookEvent.Processed = true;
                webhookEvent.ProcessedAt = DateTime.UtcNow;
                await _unitOfWork.SaveChangesAsync();
                break;
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
