using TBM.Core.Entities.Common;

namespace TBM.Core.Entities.Payments;

public class WebhookEvent : AuditableEntity
{
    public string Provider { get; set; } = "Paystack";
    public string EventType { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public bool Processed { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
