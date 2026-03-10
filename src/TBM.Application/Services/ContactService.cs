using System.Net;
using Microsoft.Extensions.Configuration;
using TBM.Application.DTOs.Common;
using TBM.Application.DTOs.Contact;
using TBM.Application.DTOs.Settings;
using TBM.Application.Interfaces;
using TBM.Core.Entities.Contact;
using TBM.Core.Interfaces.Repositories;
using TBM.Core.Interfaces.Services;

namespace TBM.Application.Services;

public class ContactService : IContactService
{
    private readonly IContactMessageRepository _contactMessages;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ISettingsManager _settingsManager;

    public ContactService(
        IContactMessageRepository contactMessages,
        IEmailService emailService,
        IConfiguration configuration,
        ISettingsManager settingsManager)
    {
        _contactMessages = contactMessages;
        _emailService = emailService;
        _configuration = configuration;
        _settingsManager = settingsManager;
    }

    public async Task<ApiResponse<ContactSubmissionResultDto>> SubmitAsync(CreateContactMessageDto dto)
    {
        var message = new ContactMessage
        {
            FullName = dto.FullName.Trim(),
            Email = dto.Email.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber.Trim(),
            Subject = string.IsNullOrWhiteSpace(dto.Subject) ? "Contact form submission" : dto.Subject.Trim(),
            Message = dto.Message.Trim(),
            EmailSent = false
        };

        await _contactMessages.CreateAsync(message);

        try
        {
            var supportEmail = await ResolveSupportEmailAsync();
            if (string.IsNullOrWhiteSpace(supportEmail))
            {
                throw new InvalidOperationException("Support email is not configured.");
            }

            var subject = $"Contact Form: {message.Subject}";
            var body = BuildSupportEmailBody(message);
            await _emailService.SendEmailAsync(supportEmail, subject, body);

            message.EmailSent = true;
            message.EmailError = null;
            await _contactMessages.UpdateAsync(message);
        }
        catch (Exception ex)
        {
            message.EmailSent = false;
            message.EmailError = ex.Message.Length > 2000
                ? ex.Message[..2000]
                : ex.Message;

            await _contactMessages.UpdateAsync(message);
        }

        return ApiResponse<ContactSubmissionResultDto>.SuccessResponse(
            new ContactSubmissionResultDto
            {
                Accepted = true,
                ReferenceId = message.Id
            },
            "Contact request accepted");
    }

    private async Task<string?> ResolveSupportEmailAsync()
    {
        var generalSettings = await _settingsManager.GetAsync<GeneralSettingsDto>("General");
        if (!string.IsNullOrWhiteSpace(generalSettings?.SupportEmail))
        {
            return generalSettings.SupportEmail.Trim();
        }

        var configured = _configuration["Contact:SupportEmail"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim();
        }

        return _configuration["SmtpSettings:FromEmail"]?.Trim();
    }

    private static string BuildSupportEmailBody(ContactMessage message)
    {
        return $"""
                <h2>New Contact Message</h2>
                <p><strong>Reference:</strong> {message.Id}</p>
                <p><strong>Name:</strong> {WebUtility.HtmlEncode(message.FullName)}</p>
                <p><strong>Email:</strong> {WebUtility.HtmlEncode(message.Email)}</p>
                <p><strong>Phone:</strong> {WebUtility.HtmlEncode(message.PhoneNumber ?? "-")}</p>
                <p><strong>Subject:</strong> {WebUtility.HtmlEncode(message.Subject ?? "-")}</p>
                <hr />
                <p>{WebUtility.HtmlEncode(message.Message).Replace("\n", "<br />")}</p>
                """;
    }
}
