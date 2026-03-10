using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TBM.Application.Services;

public class PaystackService
{
    private const string DefaultBaseUrl = "https://api.paystack.co";
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaystackService> _logger;

    public PaystackService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<PaystackService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<PaystackInitializeResult> InitializeTransactionAsync(
        PaystackInitializeRequest request,
        CancellationToken cancellationToken = default)
    {
        var secretKey = GetSecretKey();
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            return PaystackInitializeResult.Failure("Paystack secret key is not configured.");
        }

        if (string.IsNullOrWhiteSpace(request.Reference))
        {
            return PaystackInitializeResult.Failure("Payment reference is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return PaystackInitializeResult.Failure("Customer email is required.");
        }

        var payload = new
        {
            email = request.Email,
            amount = (int)Math.Round(request.Amount * 100m, MidpointRounding.AwayFromZero),
            reference = request.Reference,
            currency = string.IsNullOrWhiteSpace(request.Currency) ? GetCurrency() : request.Currency.Trim().ToUpperInvariant(),
            callback_url = string.IsNullOrWhiteSpace(request.CallbackUrl) ? null : request.CallbackUrl,
            metadata = request.Metadata
        };

        var httpRequest = BuildRequest(HttpMethod.Post, "/transaction/initialize", secretKey, payload);

        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Paystack initialize failed. Status={StatusCode}, Body={Body}",
                    (int)response.StatusCode,
                    body);
                return PaystackInitializeResult.Failure("Unable to initialize Paystack transaction.");
            }

            var parsed = JsonSerializer.Deserialize<PaystackInitializeApiResponse>(body, JsonOptions);
            if (parsed?.Status != true || parsed.Data == null)
            {
                return PaystackInitializeResult.Failure(parsed?.Message ?? "Paystack initialize returned invalid response.");
            }

            return PaystackInitializeResult.SuccessResult(
                parsed.Message ?? "Paystack transaction initialized.",
                parsed.Data.Reference ?? request.Reference,
                parsed.Data.AuthorizationUrl,
                parsed.Data.AccessCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Paystack initialize threw an exception for reference {Reference}", request.Reference);
            return PaystackInitializeResult.Failure("Unable to initialize Paystack transaction.");
        }
    }

    public async Task<PaystackVerificationResult> VerifyTransactionAsync(
        string reference,
        CancellationToken cancellationToken = default)
    {
        var secretKey = GetSecretKey();
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            return PaystackVerificationResult.Failure("Paystack secret key is not configured.");
        }

        if (string.IsNullOrWhiteSpace(reference))
        {
            return PaystackVerificationResult.Failure("Payment reference is required.");
        }

        var safeReference = Uri.EscapeDataString(reference.Trim());
        var httpRequest = BuildRequest(HttpMethod.Get, $"/transaction/verify/{safeReference}", secretKey);

        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Paystack verify failed. Reference={Reference}, Status={StatusCode}, Body={Body}",
                    reference,
                    (int)response.StatusCode,
                    body);
                return PaystackVerificationResult.Failure("Unable to verify Paystack transaction.");
            }

            var parsed = JsonSerializer.Deserialize<PaystackVerifyApiResponse>(body, JsonOptions);
            if (parsed?.Status != true || parsed.Data == null)
            {
                return PaystackVerificationResult.Failure(parsed?.Message ?? "Paystack verify returned invalid response.");
            }

            return PaystackVerificationResult.SuccessResult(
                message: parsed.Message ?? "Paystack transaction verified.",
                reference: parsed.Data.Reference ?? reference,
                status: parsed.Data.Status ?? "unknown",
                amountKobo: parsed.Data.Amount,
                currency: parsed.Data.Currency ?? GetCurrency(),
                gatewayResponse: parsed.Data.GatewayResponse,
                paidAtUtc: ParseDateTime(parsed.Data.PaidAt));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Paystack verify threw an exception for reference {Reference}", reference);
            return PaystackVerificationResult.Failure("Unable to verify Paystack transaction.");
        }
    }

    public async Task<bool> RefundAsync(
        string transactionReference,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        var secretKey = GetSecretKey();
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(transactionReference))
        {
            return false;
        }

        var payload = new
        {
            transaction = transactionReference,
            amount = (int)Math.Round(amount * 100m, MidpointRounding.AwayFromZero)
        };

        var httpRequest = BuildRequest(HttpMethod.Post, "/refund", secretKey, payload);

        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = JsonSerializer.Deserialize<PaystackBooleanApiResponse>(body, JsonOptions);
            return parsed?.Status == true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Paystack refund failed for reference {Reference}", transactionReference);
            return false;
        }
    }

    public string? GetPublicKey()
    {
        return FirstConfiguredValue("Paystack:PublicKey", "Payment:Paystack:PublicKey");
    }

    public string GetCurrency()
    {
        return (FirstConfiguredValue("Paystack:Currency", "Payment:Paystack:Currency") ?? "NGN")
            .Trim()
            .ToUpperInvariant();
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string path, string secretKey, object? payload = null)
    {
        var baseUrl = FirstConfiguredValue("Paystack:BaseUrl", "Payment:Paystack:BaseUrl") ?? DefaultBaseUrl;
        var request = new HttpRequestMessage(method, $"{baseUrl.TrimEnd('/')}{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);

        if (payload != null)
        {
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");
        }

        return request;
    }

    private string? GetSecretKey()
    {
        return FirstConfiguredValue("Paystack:SecretKey", "Payment:Paystack:SecretKey");
    }

    private string? FirstConfiguredValue(params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = _configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static DateTime? ParseDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTime.TryParse(value, out var parsed)
            ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc).ToUniversalTime()
            : null;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class PaystackInitializeApiResponse
    {
        public bool Status { get; set; }
        public string? Message { get; set; }
        public PaystackInitializeData? Data { get; set; }
    }

    private sealed class PaystackInitializeData
    {
        [JsonPropertyName("authorization_url")]
        public string? AuthorizationUrl { get; set; }

        [JsonPropertyName("access_code")]
        public string? AccessCode { get; set; }

        public string? Reference { get; set; }
    }

    private sealed class PaystackVerifyApiResponse
    {
        public bool Status { get; set; }
        public string? Message { get; set; }
        public PaystackVerifyData? Data { get; set; }
    }

    private sealed class PaystackVerifyData
    {
        public int Amount { get; set; }
        public string? Currency { get; set; }

        [JsonPropertyName("gateway_response")]
        public string? GatewayResponse { get; set; }

        [JsonPropertyName("paid_at")]
        public string? PaidAt { get; set; }

        public string? Reference { get; set; }
        public string? Status { get; set; }
    }

    private sealed class PaystackBooleanApiResponse
    {
        public bool Status { get; set; }
    }
}

public sealed class PaystackInitializeRequest
{
    public string Email { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string? Currency { get; set; }
    public string? CallbackUrl { get; set; }
    public object? Metadata { get; set; }
}

public sealed class PaystackInitializeResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string Reference { get; init; } = string.Empty;
    public string? AuthorizationUrl { get; init; }
    public string? AccessCode { get; init; }

    public static PaystackInitializeResult Failure(string message) => new()
    {
        Success = false,
        Message = message
    };

    public static PaystackInitializeResult SuccessResult(
        string message,
        string reference,
        string? authorizationUrl,
        string? accessCode) => new()
    {
        Success = true,
        Message = message,
        Reference = reference,
        AuthorizationUrl = authorizationUrl,
        AccessCode = accessCode
    };
}

public sealed class PaystackVerificationResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string Reference { get; init; } = string.Empty;
    public string Status { get; init; } = "unknown";
    public int AmountKobo { get; init; }
    public decimal Amount => AmountKobo / 100m;
    public string Currency { get; init; } = "NGN";
    public string? GatewayResponse { get; init; }
    public DateTime? PaidAtUtc { get; init; }

    public static PaystackVerificationResult Failure(string message) => new()
    {
        Success = false,
        Message = message
    };

    public static PaystackVerificationResult SuccessResult(
        string message,
        string reference,
        string status,
        int amountKobo,
        string currency,
        string? gatewayResponse,
        DateTime? paidAtUtc) => new()
    {
        Success = true,
        Message = message,
        Reference = reference,
        Status = status,
        AmountKobo = amountKobo,
        Currency = currency,
        GatewayResponse = gatewayResponse,
        PaidAtUtc = paidAtUtc
    };
}
