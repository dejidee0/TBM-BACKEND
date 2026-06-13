namespace TBM.Application.Services.Subscriptions;

public sealed class SubscriptionQuotaException : Exception
{
    public string ErrorCode { get; }

    public SubscriptionQuotaException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }
}
