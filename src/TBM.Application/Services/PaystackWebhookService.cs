using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TBM.Core.Interfaces;
using TBM.Core.Entities.Payments;
using TBM.Core.Enums;
using Microsoft.Extensions.Configuration;

public class PaystackWebhookService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;

    public PaystackWebhookService(
        IUnitOfWork unitOfWork,
        IConfiguration configuration)
    {
        _unitOfWork = unitOfWork;
        _configuration = configuration;
    }

    public bool IsValidSignature(string payload, string signature)
    {
        var secret = _configuration["Paystack:SecretKey"];
        var encoding = new UTF8Encoding();
        var keyBytes = encoding.GetBytes(secret!);
        var payloadBytes = encoding.GetBytes(payload);

        using var hmac = new HMACSHA512(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        var computedSignature = BitConverter.ToString(hash).Replace("-", "").ToLower();

        return computedSignature == signature.ToLower();
    }

    public async Task HandleAsync(string payload)
    {
        var json = JsonDocument.Parse(payload);

        var eventType = json.RootElement.GetProperty("event").GetString();
        var reference = json.RootElement
            .GetProperty("data")
            .GetProperty("reference")
            .GetString();

        if (reference == null)
            return;

        // Idempotency Check
        var existing = await _unitOfWork.WebhookEvents
            .GetByReferenceAsync(reference);

        if (existing != null && existing.Processed)
            return;

        var webhookEvent = new WebhookEvent
        {
            EventType = eventType!,
            Reference = reference,
            Payload = payload,
            Processed = false,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.WebhookEvents.AddAsync(webhookEvent);
        await _unitOfWork.SaveChangesAsync();

        // 🎯 Handle Successful Payment
        if (eventType == "charge.success")
        {
            var order = await _unitOfWork.Orders
                .GetByOrderNumberAsync(reference);

            if (order != null)
            {
                order.PaymentStatus = PaymentStatus.Paid;
                order.Status = OrderStatus.Processing;
                order.PaidAt = DateTime.UtcNow;

                webhookEvent.Processed = true;
                webhookEvent.ProcessedAt = DateTime.UtcNow;

                await _unitOfWork.SaveChangesAsync();
            }
        }
    }
}
