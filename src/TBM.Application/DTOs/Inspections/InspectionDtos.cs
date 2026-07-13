namespace TBM.Application.DTOs.Inspections;

public class VerifyInspectionPaymentRequestDto
{
    public string Reference { get; set; } = string.Empty;
}

public class VerifyInspectionPaymentResponseDto
{
    public bool Success { get; set; }
    public bool Verified { get; set; }
    public decimal Amount { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateTime? PaidAt { get; set; }
}

public class BookInspectionRequestDto
{
    public string ContactName { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string SiteAddress { get; set; } = string.Empty;
    public string SiteCity { get; set; } = string.Empty;
    public string SiteState { get; set; } = string.Empty;
    public DateTime PreferredDate1 { get; set; }
    public DateTime? PreferredDate2 { get; set; }
    public List<string>? UploadedFileUrls { get; set; }
    public string? PaymentReference { get; set; }
    public string? AdditionalNotes { get; set; }
}

public class BookInspectionResponseDto
{
    public bool Success { get; set; }
    public Guid BookingId { get; set; }
    public string Status { get; set; } = "Pending";
    public string Message { get; set; } = string.Empty;
}
