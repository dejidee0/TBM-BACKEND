namespace TBM.API.Middleware;

public sealed class RequestContextMiddleware
{
    public const string RequestIdHeaderName = "X-Request-ID";
    public const string CorrelationIdHeaderName = "X-Correlation-ID";

    private readonly RequestDelegate _next;

    public RequestContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = ResolveRequestId(context);
        context.TraceIdentifier = requestId;

        context.Response.Headers[RequestIdHeaderName] = requestId;
        context.Response.Headers[CorrelationIdHeaderName] = requestId;
        context.Items[RequestIdHeaderName] = requestId;

        await _next(context);
    }

    private static string ResolveRequestId(HttpContext context)
    {
        var incomingRequestId = context.Request.Headers[RequestIdHeaderName].ToString();
        var incomingCorrelationId = context.Request.Headers[CorrelationIdHeaderName].ToString();

        var raw = FirstNonEmpty(
            incomingRequestId,
            incomingCorrelationId,
            context.TraceIdentifier,
            Guid.NewGuid().ToString("N"));

        return NormalizeIdentifier(raw);
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return Guid.NewGuid().ToString("N");
    }

    private static string NormalizeIdentifier(string input)
    {
        var safeChars = input
            .Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_')
            .Take(64)
            .ToArray();

        if (safeChars.Length == 0)
        {
            return Guid.NewGuid().ToString("N");
        }

        return new string(safeChars);
    }
}
