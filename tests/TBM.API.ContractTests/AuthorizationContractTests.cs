using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using TBM.API.ContractTests.Infrastructure;

namespace TBM.API.ContractTests;

public sealed class AuthorizationContractTests : IClassFixture<ApiContractFactory>
{
    private readonly EndpointDataSource _endpointDataSource;

    public AuthorizationContractTests(ApiContractFactory factory)
    {
        _endpointDataSource = factory.Services.GetRequiredService<EndpointDataSource>();
    }

    [Theory]
    [InlineData("GET", "api/v1/checkout/payment/paystack/verify/{reference}")]
    [InlineData("GET", "api/v1/account/profile")]
    [InlineData("POST", "api/v1/cart/merge")]
    [InlineData("GET", "api/v1/ai/projects")]
    [InlineData("POST", "api/v1/ai/generate/image")]
    [InlineData("GET", "api/v1/vendor/orders/export")]
    [InlineData("POST", "api/v1/vendor/orders/import")]
    public void Protected_Endpoints_Should_Require_Authorization(string method, string routePattern)
    {
        var endpoint = FindRouteEndpoint(method, routePattern);
        Assert.NotNull(endpoint);

        var hasAuthorize = endpoint!.Metadata.Any(m => m is AuthorizeAttribute);
        Assert.True(hasAuthorize);
    }

    [Theory]
    [InlineData("GET", "api/v1/public/projects")]
    [InlineData("POST", "api/v1/contact")]
    [InlineData("GET", "api/v1/auth/providers")]
    [InlineData("GET", "api/v1/checkout")]
    [InlineData("POST", "api/v1/checkout/validate-promo")]
    [InlineData("POST", "api/v1/checkout/payment")]
    [InlineData("POST", "api/v1/orders")]
    public void Public_Endpoints_Should_Be_AllowAnonymous(string method, string routePattern)
    {
        var endpoint = FindRouteEndpoint(method, routePattern);
        Assert.NotNull(endpoint);

        var hasAllowAnonymous = endpoint!.Metadata.Any(m => m is AllowAnonymousAttribute);
        Assert.True(hasAllowAnonymous);
    }

    [Fact]
    public void Admin_Observability_Endpoint_Should_Require_Admin_Role()
    {
        var endpoint = FindRouteEndpoint("GET", "api/v1/admin/observability/slo/overview");
        Assert.NotNull(endpoint);

        var authorize = endpoint!.Metadata
            .OfType<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal("Admin,SuperAdmin", authorize!.Roles);
    }

    [Fact]
    public void Vendor_Endpoints_Should_Require_Vendor_Role()
    {
        // export is GET, import is POST
        var exportEndpoint = FindRouteEndpoint("GET", "api/v1/vendor/orders/export");
        Assert.NotNull(exportEndpoint);
        var importEndpoint = FindRouteEndpoint("POST", "api/v1/vendor/orders/import");
        Assert.NotNull(importEndpoint);

        foreach (var endpoint in new[] { exportEndpoint, importEndpoint })
        {
            var authorize = endpoint!.Metadata
                .OfType<AuthorizeAttribute>()
                .FirstOrDefault();

            Assert.NotNull(authorize);
            Assert.Contains("Vendor", authorize!.Roles);
        }
    }

    private RouteEndpoint? FindRouteEndpoint(string method, string routePattern)
    {
        return _endpointDataSource.Endpoints
            .OfType<RouteEndpoint>()
            .FirstOrDefault(endpoint =>
            {
                var route = endpoint.RoutePattern.RawText ?? string.Empty;
                if (!string.Equals(route, routePattern, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                var methods = endpoint.Metadata
                    .OfType<HttpMethodMetadata>()
                    .FirstOrDefault()?
                    .HttpMethods;

                return methods != null && methods.Any(m =>
                    string.Equals(m, method, StringComparison.OrdinalIgnoreCase));
            });
    }
}
