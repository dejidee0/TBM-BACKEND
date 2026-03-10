using Microsoft.Extensions.Configuration;
using TBM.Application.DTOs.Contact;
using TBM.Application.DTOs.Settings;
using TBM.Application.Interfaces;
using TBM.Application.Services;
using TBM.Core.Entities.Contact;
using TBM.Core.Interfaces.Repositories;
using TBM.Core.Interfaces.Services;

namespace TBM.API.ContractTests;

public sealed class ContactServiceTests
{
    [Fact]
    public async Task SubmitAsync_PersistsMessage_AndMarksEmailSent_WhenEmailSucceeds()
    {
        var repository = new InMemoryContactRepository();
        var email = new StubEmailService();
        var config = BuildConfig();
        var settings = new StubSettingsManager(new GeneralSettingsDto
        {
            PlatformName = "TBM",
            SupportEmail = "support@tbm.test",
            MaintenanceMode = false,
            ApiRateLimit = 600
        });

        var service = new ContactService(repository, email, config, settings);

        var result = await service.SubmitAsync(new CreateContactMessageDto
        {
            FullName = "Jane Doe",
            Email = "jane@example.com",
            Subject = "Need a quote",
            Message = "Please contact me."
        });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data!.Accepted);

        var persisted = await repository.GetByIdAsync(result.Data.ReferenceId);
        Assert.NotNull(persisted);
        Assert.True(persisted!.EmailSent);
        Assert.Null(persisted.EmailError);
    }

    [Fact]
    public async Task SubmitAsync_PersistsMessage_WhenEmailFails()
    {
        var repository = new InMemoryContactRepository();
        var email = new StubEmailService { ThrowOnSend = true };
        var config = BuildConfig();
        var settings = new StubSettingsManager(new GeneralSettingsDto
        {
            PlatformName = "TBM",
            SupportEmail = "support@tbm.test",
            MaintenanceMode = false,
            ApiRateLimit = 600
        });

        var service = new ContactService(repository, email, config, settings);

        var result = await service.SubmitAsync(new CreateContactMessageDto
        {
            FullName = "John Doe",
            Email = "john@example.com",
            Subject = "Question",
            Message = "I have a question."
        });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data!.Accepted);

        var persisted = await repository.GetByIdAsync(result.Data.ReferenceId);
        Assert.NotNull(persisted);
        Assert.False(persisted!.EmailSent);
        Assert.False(string.IsNullOrWhiteSpace(persisted.EmailError));
    }

    private static IConfiguration BuildConfig()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SmtpSettings:FromEmail"] = "noreply@tbm.test"
            })
            .Build();
    }

    private sealed class InMemoryContactRepository : IContactMessageRepository
    {
        private readonly Dictionary<Guid, ContactMessage> _store = new();

        public Task<ContactMessage> CreateAsync(ContactMessage message)
        {
            _store[message.Id] = message;
            return Task.FromResult(message);
        }

        public Task<ContactMessage?> GetByIdAsync(Guid id)
        {
            _store.TryGetValue(id, out var message);
            return Task.FromResult(message);
        }

        public Task UpdateAsync(ContactMessage message)
        {
            _store[message.Id] = message;
            return Task.CompletedTask;
        }
    }

    private sealed class StubEmailService : IEmailService
    {
        public bool ThrowOnSend { get; set; }

        public Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            if (ThrowOnSend)
            {
                throw new InvalidOperationException("SMTP failure");
            }

            return Task.CompletedTask;
        }

        public Task SendVerificationEmailAsync(string toEmail, string fullName, string verificationLink) => Task.CompletedTask;
        public Task SendPasswordResetEmailAsync(string toEmail, string fullName, string resetLink) => Task.CompletedTask;
        public Task SendPasswordResetConfirmationAsync(string toEmail, string fullName) => Task.CompletedTask;
        public Task SendPasswordChangeOTPAsync(string toEmail, string fullName, string otp) => Task.CompletedTask;
        public Task SendWelcomeEmailAsync(string toEmail, string fullName) => Task.CompletedTask;
        public Task SendOrderConfirmationAsync(string toEmail, string fullName, string orderNumber, decimal totalAmount) => Task.CompletedTask;
    }

    private sealed class StubSettingsManager : ISettingsManager
    {
        private readonly GeneralSettingsDto _generalSettings;

        public StubSettingsManager(GeneralSettingsDto generalSettings)
        {
            _generalSettings = generalSettings;
        }

        public Task<T?> GetAsync<T>(string category) where T : class
        {
            if (typeof(T) == typeof(GeneralSettingsDto) &&
                string.Equals(category, "General", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(_generalSettings as T);
            }

            return Task.FromResult<T?>(null);
        }

        public Task SaveAsync<T>(string category, T value) where T : class => Task.CompletedTask;

        public Task RefreshAsync(string category) => Task.CompletedTask;
    }
}
