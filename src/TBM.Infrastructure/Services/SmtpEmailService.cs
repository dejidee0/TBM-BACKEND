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


        public Task SendPasswordChangeOTPAsync(string toEmail, string fullName, string otp)
{
    var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ 
            font-family: Arial, sans-serif; 
            line-height: 1.6; 
            color: #333;
            background-color: #f4f4f4;
        }}
        .container {{ 
            max-width: 600px; 
            margin: 30px auto; 
            background: white;
            border-radius: 10px;
            overflow: hidden;
            box-shadow: 0 4px 6px rgba(0,0,0,0.1);
        }}
        .header {{ 
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white; 
            padding: 30px 20px; 
            text-align: center;
        }}
        .content {{ 
            padding: 40px 30px;
        }}
        .otp-box {{
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            font-size: 32px;
            font-weight: bold;
            letter-spacing: 8px;
            padding: 20px;
            text-align: center;
            border-radius: 10px;
            margin: 30px 0;
            font-family: 'Courier New', monospace;
        }}
        .warning {{ 
            background-color: #fff3cd; 
            border-left: 4px solid #ffc107; 
            padding: 15px; 
            margin: 25px 0;
            border-radius: 4px;
        }}
        .footer {{ 
            text-align: center; 
            padding: 20px; 
            background-color: #f8f9fa;
            color: #6c757d; 
            font-size: 12px;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🔐 Password Change Request</h1>
        </div>
        <div class='content'>
            <p>Hello <strong>{fullName}</strong>,</p>
            <p>You requested to change your password. Use the OTP code below to complete the process:</p>
            
            <div class='otp-box'>
                {otp}
            </div>
            
            <p style='text-align: center; color: #666; margin-top: -15px;'>
                This code will expire in <strong>10 minutes</strong>
            </p>
            
            <div class='warning'>
                <strong>⚠️ Security Notice:</strong>
                <ul style='margin: 10px 0; padding-left: 20px;'>
                    <li>Never share this OTP with anyone</li>
                    <li>TBM staff will never ask for your OTP</li>
                    <li>If you didn't request this, ignore this email</li>
                </ul>
            </div>
            
            <p style='margin-top: 30px; color: #666; font-size: 14px;'>
                If you're having trouble, contact support at support@buildtbm.com
            </p>
        </div>
        <div class='footer'>
            <p>© 2026 TBM Building Services. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";

    return SendEmailAsync(toEmail, "Your Password Change OTP - TBM Building Services", body);
}

       public Task SendPasswordResetEmailAsync(string toEmail, string fullName, string resetLink)
{
    var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ 
            font-family: Arial, sans-serif; 
            line-height: 1.6; 
            color: #333;
            background-color: #f4f4f4;
            margin: 0;
            padding: 0;
        }}
        .container {{ 
            max-width: 600px; 
            margin: 30px auto; 
            background: white;
            border-radius: 10px;
            overflow: hidden;
            box-shadow: 0 4px 6px rgba(0,0,0,0.1);
        }}
        .header {{ 
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white; 
            padding: 30px 20px; 
            text-align: center;
        }}
        .header h1 {{
            margin: 0;
            font-size: 24px;
        }}
        .content {{ 
            padding: 40px 30px;
            background-color: #ffffff;
        }}
        .content h2 {{
            color: #667eea;
            margin-top: 0;
        }}
        .button {{ 
            display: inline-block; 
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white !important; 
            padding: 15px 40px; 
            text-decoration: none; 
            border-radius: 50px; 
            margin: 25px 0;
            font-weight: bold;
            text-align: center;
        }}
        .button:hover {{
            opacity: 0.9;
        }}
        .warning {{ 
            background-color: #fff3cd; 
            border-left: 4px solid #ffc107; 
            padding: 15px; 
            margin: 25px 0;
            border-radius: 4px;
        }}
        .footer {{ 
            text-align: center; 
            padding: 20px; 
            background-color: #f8f9fa;
            color: #6c757d; 
            font-size: 12px;
        }}
        .link-text {{
            word-break: break-all;
            color: #667eea;
            font-size: 12px;
            margin-top: 15px;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🔐 TBM Building Services</h1>
        </div>
        <div class='content'>
            <h2>Password Reset Request</h2>
            <p>Hello <strong>{fullName}</strong>,</p>
            <p>We received a request to reset your password for your TBM account.</p>
            <p>Click the button below to reset your password:</p>
            
            <div style='text-align: center;'>
                <a href='{resetLink}' class='button'>Reset My Password</a>
            </div>
            
            <p class='link-text'>Or copy and paste this link into your browser:<br>{resetLink}</p>
            
            <div class='warning'>
                <strong>⚠️ Security Notice:</strong>
                <ul style='margin: 10px 0; padding-left: 20px;'>
                    <li>This link will expire in <strong>1 hour</strong></li>
                    <li>If you didn't request this, please ignore this email</li>
                    <li>Your password won't change until you create a new one</li>
                </ul>
            </div>
            
            <p style='margin-top: 30px; color: #6c757d;'>
                If you're having trouble clicking the button, copy and paste the URL above into your web browser.
            </p>
        </div>
        <div class='footer'>
            <p>© 2026 TBM Building Services. All rights reserved.</p>
            <p>Need help? Contact us at <a href='mailto:support@buildtbm.com'>support@buildtbm.com</a></p>
        </div>
    </div>
</body>
</html>";

    return SendEmailAsync(toEmail, "Reset Your Password - TBM Building Services", body);
}


public Task SendPasswordResetConfirmationAsync(string toEmail, string fullName)
{
    var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ 
            font-family: Arial, sans-serif; 
            line-height: 1.6; 
            color: #333;
            background-color: #f4f4f4;
        }}
        .container {{ 
            max-width: 600px; 
            margin: 30px auto; 
            background: white;
            border-radius: 10px;
            overflow: hidden;
            box-shadow: 0 4px 6px rgba(0,0,0,0.1);
        }}
        .header {{ 
            background: linear-gradient(135deg, #10b981 0%, #059669 100%);
            color: white; 
            padding: 30px 20px; 
            text-align: center;
        }}
        .content {{ 
            padding: 40px 30px;
        }}
        .success-icon {{
            font-size: 48px;
            text-align: center;
            margin-bottom: 20px;
        }}
        .footer {{ 
            text-align: center; 
            padding: 20px; 
            background-color: #f8f9fa;
            color: #6c757d; 
            font-size: 12px;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>✓ Password Updated Successfully</h1>
        </div>
        <div class='content'>
            <div class='success-icon'>🎉</div>
            <p>Hello <strong>{fullName}</strong>,</p>
            <p>Your password has been successfully reset.</p>
            <p>You can now login to your TBM account with your new password.</p>
            <p style='margin-top: 30px; padding: 15px; background-color: #fff3cd; border-left: 4px solid #ffc107;'>
                <strong>⚠️ Security Alert:</strong><br>
                If you did not make this change, please contact us immediately at 
                <a href='mailto:support@buildtbm.com'>support@buildtbm.com</a>
            </p>
        </div>
        <div class='footer'>
            <p>© 2026 TBM Building Services. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";

    return SendEmailAsync(toEmail, "Password Reset Successful - TBM Building Services", body);
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
