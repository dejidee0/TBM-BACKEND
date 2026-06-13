using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

public class PaystackService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public PaystackService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<bool> RefundAsync(string transactionReference, decimal amount)
    {
        var secretKey = _configuration["Payment:Paystack:SecretKey"];

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", secretKey);

        var payload = new
        {
            transaction = transactionReference,
            amount = (int)(amount * 100) // kobo
        };

        var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync(
            "https://api.paystack.co/refund",
            content);

        return response.IsSuccessStatusCode;
    }
}
