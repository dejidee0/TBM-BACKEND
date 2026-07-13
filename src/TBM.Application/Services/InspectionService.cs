using System.Text.Json;
using TBM.Application.DTOs.Inspections;
using TBM.Core.Entities.Inspections;
using TBM.Core.Interfaces.Repositories;

namespace TBM.Application.Services;

public class InspectionService
{
    private readonly IInspectionRequestRepository _inspectionRequests;
    private readonly PaystackService _paystackService;

    public InspectionService(IInspectionRequestRepository inspectionRequests, PaystackService paystackService)
    {
        _inspectionRequests = inspectionRequests;
        _paystackService = paystackService;
    }

    public async Task<VerifyInspectionPaymentResponseDto> VerifyPaymentAsync(string reference)
    {
        var result = await _paystackService.VerifyTransactionAsync(reference);

        return new VerifyInspectionPaymentResponseDto
        {
            Success = result.Success,
            Verified = result.Success && string.Equals(result.Status, "success", StringComparison.OrdinalIgnoreCase),
            Amount = result.Amount,
            Reference = result.Reference,
            PaidAt = result.PaidAtUtc
        };
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
