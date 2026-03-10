using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TBM.Application.DTOs.Settings;
using TBM.Application.Interfaces;

namespace TBM.API.ContractTests.Infrastructure;

public sealed class ApiContractFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=(localdb)\\mssqllocaldb;Database=TBM_ContractTests;Trusted_Connection=True;TrustServerCertificate=True;",
                ["Security:EncryptionKey"] = "TBM_Contract_Test_Encryption_Key_32_Chars",
                ["JwtSettings:SecretKey"] =
                    "TBM_Super_Secret_Key_2025_Change_This_In_Production_Min_32_Chars",
                ["JwtSettings:Issuer"] = "TBMDigitalPlatform",
                ["JwtSettings:Audience"] = "TBMUsers",
                ["Cors:AllowedOrigins:0"] = "http://localhost:3000",
                ["Paystack:BaseUrl"] = "https://api.paystack.co",
                ["Paystack:SecretKey"] = "sk_test_dummy_contract_key",
                ["Paystack:PublicKey"] = "pk_test_dummy_contract_key",
                ["Paystack:Currency"] = "NGN"
            };

            configBuilder.AddInMemoryCollection(overrides);
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(x => x.ServiceType == typeof(ISettingsManager));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddSingleton<ISettingsManager, ContractSettingsManager>();
        });
    }

    private sealed class ContractSettingsManager : ISettingsManager
    {
        public Task<T?> GetAsync<T>(string category) where T : class
        {
            if (typeof(T) == typeof(GeneralSettingsDto) &&
                string.Equals(category, "General", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new GeneralSettingsDto
                {
                    PlatformName = "TBM",
                    SupportEmail = "support@tbm.test",
                    MaintenanceMode = false,
                    ApiRateLimit = 1000
                } as T);
            }

            return Task.FromResult<T?>(null);
        }

        public Task SaveAsync<T>(string category, T value) where T : class => Task.CompletedTask;

        public Task RefreshAsync(string category) => Task.CompletedTask;
    }
}
