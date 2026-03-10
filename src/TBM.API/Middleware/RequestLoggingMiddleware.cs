using System.Diagnostics;
using System.Security.Claims;
using TBM.API.Observability;

namespace TBM.API.Middleware;

public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    private readonly IEndpointMetricsStore _metricsStore;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger,
        IEndpointMetricsStore metricsStore)
    {
        _next = next;
        _logger = logger;
        _metricsStore = metricsStore;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var startedAt = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "/";
        var requestId = context.TraceIdentifier;
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
        var client = ResolveClient(context);
        var domain = ResolveDomain(path);

        try
        {
            await _next(context);
            stopwatch.Stop();

            var elapsedMs = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2);
            _metricsStore.Record(domain, method, path, context.Response.StatusCode, elapsedMs, startedAt);

            _logger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms RequestId={RequestId} UserId={UserId} Client={Client}",
                method,
                path,
                context.Response.StatusCode,
                elapsedMs,
                requestId,
                userId,
                client);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var elapsedMs = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2);
            _metricsStore.Record(domain, method, path, StatusCodes.Status500InternalServerError, elapsedMs, startedAt);

            _logger.LogError(
                ex,
                "HTTP {Method} {Path} failed in {ElapsedMs}ms RequestId={RequestId} UserId={UserId} Client={Client}",
                method,
                path,
                elapsedMs,
                requestId,
                userId,
                client);

            throw;
        }
    }

    private static string ResolveDomain(string path)
    {
        var normalized = path.ToLowerInvariant();

        if (normalized.StartsWith("/api/v1/auth", StringComparison.Ordinal) ||
            normalized.StartsWith("/auth", StringComparison.Ordinal) ||
            normalized.StartsWith("/api/auth", StringComparison.Ordinal) ||
            normalized.StartsWith("/api/admin/auth", StringComparison.Ordinal))
        {
            return "auth";
        }

        if (normalized.StartsWith("/api/v1/checkout", StringComparison.Ordinal) ||
            normalized.StartsWith("/api/checkout", StringComparison.Ordinal))
        {
            return "checkout";
        }

        if (normalized.StartsWith("/api/v1/ai", StringComparison.Ordinal))
        {
            return "ai";
        }

        return "other";
    }

    private static string ResolveClient(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            var first = forwardedFor.Split(',')[0].Trim();
            if (!string.IsNullOrWhiteSpace(first))
            {
                return first;
            }
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
