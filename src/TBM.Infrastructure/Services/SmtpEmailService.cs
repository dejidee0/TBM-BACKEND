using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using TBM.Core.Interfaces.Services;
using TBM.Infrastructure.Configuration;

namespace TBM.Infrastructure.Services
{
    public class SmtpEmailService : IEmailService
    {
        private readonly SmtpSettings _settings;

        public SmtpEmailService(IOptions<SmtpSettings> settings)
        {
            _settings = settings.Value;
        }

        private SmtpClient CreateClient()
        {
            // Force TLS 1.2 (required for Brevo)
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                Credentials = new NetworkCredential(_settings.Username, _settings.Password),
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 20000
            };

            return client;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            try
            {
                var message = new MailMessage
                {
                    From = new MailAddress(_settings.FromEmail, _settings.FromName),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };

                message.To.Add(toEmail);

                using var client = CreateClient();
                await client.SendMailAsync(message);
            }
            catch (Exception ex)
            {
                throw new Exception($"SMTP Error: {ex.Message}", ex);
            }
        }

        public Task SendVerificationEmailAsync(string toEmail, string fullName, string verificationLink)
        {
            var body = $@"
                <h2>Welcome to TBM Digital Platform</h2>
                <p>Hello {fullName},</p>
                <p>Please verify your email:</p>
                <a href='{verificationLink}'>Verify Email</a>";

            return SendEmailAsync(toEmail, "Verify Your Email - TBM", body);
        }

        public Task SendPasswordResetEmailAsync(string toEmail, string fullName, string resetLink)
        {
            var body = $@"
                <h2>Password Reset</h2>
                <p>Hello {fullName},</p>
                <p>Click below to reset your password:</p>
                <a href='{resetLink}'>Reset Password</a>";

            return SendEmailAsync(toEmail, "Reset Your Password - TBM", body);
        }

        public Task SendWelcomeEmailAsync(string toEmail, string fullName)
        {
            var body = $@"
                <h2>Welcome to TBM Digital Platform</h2>
                <p>Hello {fullName},</p>
                <p>Your account has been created successfully.</p>";

            return SendEmailAsync(toEmail, "Welcome to TBM", body);
        }

        public Task SendOrderConfirmationAsync(string toEmail, string fullName, string orderNumber, decimal totalAmount)
        {
            var body = $@"
                <h2>Order Confirmation</h2>
                <p>Hello {fullName},</p>
                <p>Your order <b>{orderNumber}</b> has been received.</p>
                <p>Total: ₦{totalAmount:N2}</p>";

            return SendEmailAsync(toEmail, "Order Confirmation - TBM", body);
        }
    }
}
