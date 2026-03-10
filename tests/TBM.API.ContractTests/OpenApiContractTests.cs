using System.Text.Json;
using TBM.API.ContractTests.Infrastructure;

namespace TBM.API.ContractTests;

public sealed class OpenApiContractTests : IClassFixture<ApiContractFactory>
{
    private readonly HttpClient _client;

    public OpenApiContractTests(ApiContractFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SwaggerDocument_Exposes_Critical_Paths_And_Bearer_Security()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        var root = document.RootElement;
        var paths = root.GetProperty("paths")
            .EnumerateObject()
            .Select(x => x.Name.ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("/api/v1/auth/login", paths);
        Assert.Contains("/api/v1/auth/register", paths);
        Assert.Contains("/api/v1/checkout/payment", paths);
        Assert.Contains("/api/v1/checkout/payment/paystack/verify/{reference}", paths);
        Assert.Contains("/api/webhooks/paystack", paths);
        Assert.Contains("/api/v1/ai/generate/image", paths);
        Assert.Contains("/api/v1/materials", paths);
        Assert.Contains("/api/v1/materials/{idorslug}", paths);
        Assert.Contains("/api/v1/cart/merge", paths);
        Assert.Contains("/api/v1/public/projects", paths);
        Assert.Contains("/api/v1/contact", paths);
        Assert.Contains("/api/v1/ai/projects", paths);
        Assert.Contains("/api/v1/auth/providers", paths);
        Assert.Contains("/api/v1/vendor/orders/export", paths);
        Assert.Contains("/api/v1/vendor/orders/import", paths);
        Assert.Contains("/api/admin/observability/slo/overview", paths);

        var securitySchemes = root
            .GetProperty("components")
            .GetProperty("securitySchemes");

        Assert.True(securitySchemes.TryGetProperty("Bearer", out _));
    }
}
