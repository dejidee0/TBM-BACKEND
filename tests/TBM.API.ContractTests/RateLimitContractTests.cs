using System.Net;
using TBM.API.ContractTests.Infrastructure;

namespace TBM.API.ContractTests;

public sealed class RateLimitContractTests : IClassFixture<ApiContractFactory>
{
    private readonly HttpClient _client;

    public RateLimitContractTests(ApiContractFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AuthSensitive_Endpoint_Should_Return_429_When_Limit_Is_Exceeded()
    {
        var statuses = new List<HttpStatusCode>();

        for (var i = 0; i < 20; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/auth/google");
            request.Headers.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.42");
            var response = await _client.SendAsync(request);
            statuses.Add(response.StatusCode);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                Assert.True(response.Headers.Contains("Retry-After"));
                break;
            }
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }
}
