namespace TBM.Core.Interfaces.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string htmlBody);
        Task SendVerificationEmailAsync(string toEmail, string fullName, string verificationLink);
        Task SendPasswordResetEmailAsync(string toEmail, string fullName, string resetLink);
        Task SendWelcomeEmailAsync(string toEmail, string fullName);
        Task SendOrderConfirmationAsync(string toEmail, string fullName, string orderNumber, decimal totalAmount);
    }
}
