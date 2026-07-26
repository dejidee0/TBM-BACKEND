using System.Text.Json;
using Microsoft.Extensions.Configuration;
using TBM.Application.DTOs.Inspections;
using TBM.Core.Entities.Inspections;
using TBM.Core.Interfaces.Repositories;

namespace TBM.Application.Services;

public class InspectionService
{
    private const decimal DefaultInspectionFee = 50_000m;

    private readonly IInspectionRequestRepository _inspectionRequests;
    private readonly PaystackService _paystackService;
    private readonly IConfiguration _configuration;

    public InspectionService(
        IInspectionRequestRepository inspectionRequests,
        PaystackService paystackService,
        IConfiguration configuration)
    {
        _inspectionRequests = inspectionRequests;
        _paystackService = paystackService;
        _configuration = configuration;
    }

    /// <summary>
    /// Initializes a Paystack transaction for an inspection's fee. The amount is
    /// always the server-side fee stored on the booking (never client-supplied).
    /// Returns null when the inspection does not exist.
    /// </summary>
    public async Task<InitializeInspectionPaymentResponseDto?> InitializePaymentAsync(Guid inspectionId, string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.");
        }

        var inspection = await _inspectionRequests.GetByIdAsync(inspectionId);
        if (inspection == null)
        {
            return null;
        }

        if (inspection.PaymentVerified)
        {
            throw new InvalidOperationException("The inspection fee has already been paid.");
        }

        // Bookings created before the fee column existed carry 0 — backfill from config.
        if (inspection.InspectionFee <= 0)
        {
            inspection.InspectionFee = GetConfiguredFee();
        }

        var reference = $"INSP-{inspectionId.ToString("N")[..8]}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

        // PaystackService converts the naira amount to kobo internally.
        var result = await _paystackService.InitializeTransactionAsync(new PaystackInitializeRequest
        {
            Email = email.Trim(),
            Amount = inspection.InspectionFee,
            Reference = reference
        });

        if (!result.Success)
        {
            throw new InvalidOperationException(result.Message);
        }

        inspection.PaymentReference = result.Reference;
        await _inspectionRequests.UpdateAsync(inspection);

        return new InitializeInspectionPaymentResponseDto
        {
            AuthorizationUrl = result.AuthorizationUrl,
            AccessCode = result.AccessCode,
            Reference = result.Reference,
            Amount = inspection.InspectionFee
        };
    }

    /// <summary>
    /// Verifies an inspection fee payment against the reference and fee stored
    /// server-side. Idempotent: an already-paid inspection short-circuits to
    /// success without re-contacting Paystack.
    /// </summary>
    public async Task<VerifyInspectionPaymentResponseDto> VerifyPaymentAsync(string reference)
    {
        var inspection = await _inspectionRequests.GetByPaymentReferenceAsync(reference);
        if (inspection == null)
        {
            return new VerifyInspectionPaymentResponseDto
            {
                Success = false,
                Verified = false,
                Reference = reference,
                Message = "No inspection booking matches this payment reference."
            };
        }

        if (inspection.PaymentVerified)
        {
            return new VerifyInspectionPaymentResponseDto
            {
                Success = true,
                Verified = true,
                Amount = inspection.InspectionFee,
                Reference = reference,
                Message = "Payment already verified."
            };
        }

        var result = await _paystackService.VerifyTransactionAsync(reference);
        var paystackSaysPaid = result.Success
            && string.Equals(result.Status, "success", StringComparison.OrdinalIgnoreCase);

        if (!paystackSaysPaid)
        {
            return new VerifyInspectionPaymentResponseDto
            {
                Success = result.Success,
                Verified = false,
                Amount = result.Amount,
                Reference = reference,
                PaidAt = result.PaidAtUtc,
                Message = result.Message
            };
        }

        // Never trust the client — the paid amount must match the server-side fee.
        if (result.Amount != inspection.InspectionFee)
        {
            throw new InvalidOperationException(
                $"Paid amount (₦{result.Amount:N2}) does not match the inspection fee (₦{inspection.InspectionFee:N2}).");
        }

        inspection.PaymentVerified = true;
        await _inspectionRequests.UpdateAsync(inspection);

        return new VerifyInspectionPaymentResponseDto
        {
            Success = true,
            Verified = true,
            Amount = result.Amount,
            Reference = reference,
            PaidAt = result.PaidAtUtc,
            Message = "Payment verified."
        };
    }

    private decimal GetConfiguredFee()
    {
        var fee = _configuration.GetValue("Inspections:FeeAmount", DefaultInspectionFee);
        return fee > 0 ? fee : DefaultInspectionFee;
    }

    public async Task<BookInspectionResponseDto> BookAsync(BookInspectionRequestDto dto)
    {
        ValidateRequest(dto);

        var entity = new InspectionRequest
        {
            ContactName = dto.ContactName.Trim(),
            ContactPhone = dto.ContactPhone.Trim(),
            ContactEmail = dto.ContactEmail.Trim(),
            SiteAddress = dto.SiteAddress.Trim(),
            SiteCity = dto.SiteCity.Trim(),
            SiteState = dto.SiteState.Trim(),
            PreferredDate1 = dto.PreferredDate1,
            PreferredDate2 = dto.PreferredDate2,
            AdditionalNotes = string.IsNullOrWhiteSpace(dto.AdditionalNotes) ? null : dto.AdditionalNotes.Trim(),
            UploadedFileUrlsJson = dto.UploadedFileUrls is { Count: > 0 }
                ? JsonSerializer.Serialize(dto.UploadedFileUrls)
                : null,
            PaymentReference = string.IsNullOrWhiteSpace(dto.PaymentReference) ? null : dto.PaymentReference.Trim(),
            PaymentVerified = false,
            InspectionFee = GetConfiguredFee(),
            Status = "Pending"
        };

        await _inspectionRequests.CreateAsync(entity);

        return new BookInspectionResponseDto
        {
            Success = true,
            BookingId = entity.Id,
            Status = entity.Status,
            Message = "Inspection booked. Team will contact you within 24 hours."
        };
    }

    private static void ValidateRequest(BookInspectionRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ContactName))
        {
            throw new ArgumentException("Contact name is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.ContactPhone))
        {
            throw new ArgumentException("Contact phone is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.ContactEmail))
        {
            throw new ArgumentException("Contact email is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.SiteAddress))
        {
            throw new ArgumentException("Site address is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.SiteCity))
        {
            throw new ArgumentException("Site city is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.SiteState))
        {
            throw new ArgumentException("Site state is required.");
        }

        if (dto.PreferredDate1 == default)
        {
            throw new ArgumentException("Preferred date is required.");
        }
    }
}
