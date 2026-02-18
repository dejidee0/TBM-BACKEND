namespace TBM.Core.Enums;

public enum PaymentStatus
{
    Pending = 1,          // Awaiting payment
    Paid = 2,             // Payment successful
    Failed = 3,           // Payment failed
    Refunded = 4,         // Payment refunded
    PartiallyPaid = 5     // Partial payment received
}