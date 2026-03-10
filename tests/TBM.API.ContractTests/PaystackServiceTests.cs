using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TBM.Application.Services;

namespace TBM.API.ContractTests;

public sealed class PaystackServiceTests
{
    [Fact]
    public async Task InitializeTransactionAsync_ReturnsAuthorizationData_WhenPaystackRespondsWithSuccess()
    {
        var payload = """
                      {
                        "status": true,
                        "message": "Authorization URL created",
                        "data": {
                          "authorization_url": "https://checkout.paystack.com/abc123",
                          "access_code": "abc123",
                          "reference": "TBM-REF-001"
                        }
                      }
                      """;

        var service = CreateService(
            new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            }),
            new Dictionary<string, string?>
            {
                ["Paystack:SecretKey"] = "sk_test_key",
                ["Paystack:BaseUrl"] = "https://api.paystack.co",
                ["Paystack:Currency"] = "NGN"
            });

        var result = await service.InitializeTransactionAsync(new PaystackInitializeRequest
        {
            Email = "test@example.com",
            Amount = 15000m,
            Reference = "TBM-REF-001"
        });

        Assert.True(result.Success);
        Assert.Equal("TBM-REF-001", result.Reference);
        Assert.Equal("https://checkout.paystack.com/abc123", result.AuthorizationUrl);
        Assert.Equal("abc123", result.AccessCode);
    }

    [Fact]
    public async Task VerifyTransactionAsync_ReturnsSuccess_WhenGatewayStatusIsSuccess()
    {
        var payload = """
                      {
                        "status": true,
                        "message": "Verification successful",
                        "data": {
                          "status": "success",
                          "reference": "TBM-REF-002",
                          "amount": 2500000,
                          "currency": "NGN",
                          "gateway_response": "Successful",
                          "paid_at": "2026-03-05T10:30:00Z"
                        }
                      }
                      """;

        var service = CreateService(
            new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            }),
            new Dictionary<string, string?>
            {
                ["Paystack:SecretKey"] = "sk_test_key",
                ["Paystack:BaseUrl"] = "https://api.paystack.co",
                ["Paystack:Currency"] = "NGN"
            });

        var result = await service.VerifyTransactionAsync("TBM-REF-002");

        Assert.True(result.Success);
        Assert.Equal("TBM-REF-002", result.Reference);
        Assert.Equal("success", result.Status);
        Assert.Equal(25000m, result.Amount);
        Assert.Equal("NGN", result.Currency);
        Assert.Equal("Successful", result.GatewayResponse);
        Assert.NotNull(result.PaidAtUtc);
    }

    [Fact]
    public async Task InitializeTransactionAsync_ReturnsFailure_WhenSecretKeyIsMissing()
    {
        var service = CreateService(
            new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)),
            new Dictionary<string, string?>
            {
                ["Paystack:BaseUrl"] = "https://api.paystack.co"
            });

        var result = await service.InitializeTransactionAsync(new PaystackInitializeRequest
        {
            Email = "test@example.com",
            Amount = 1000m,
            Reference = "TBM-REF-003"
        });

        Assert.False(result.Success);
        Assert.Contains("secret key", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static PaystackService CreateService(
        HttpMessageHandler handler,
        Dictionary<string, string?> values)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var httpClient = new HttpClient(handler);
        return new PaystackService(httpClient, config, NullLogger<PaystackService>.Instance);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
