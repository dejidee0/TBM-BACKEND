using TBM.Core.Entities.Common;

namespace TBM.Core.Entities.Inspections;

public class InspectionRequest : AuditableEntity
{
    public Guid? UserId { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string SiteAddress { get; set; } = string.Empty;
    public string SiteCity { get; set; } = string.Empty;
    public string SiteState { get; set; } = string.Empty;
    public DateTime PreferredDate1 { get; set; }
    public DateTime? PreferredDate2 { get; set; }
    public string? AdditionalNotes { get; set; }
    public string? UploadedFileUrlsJson { get; set; }
    public string? PaymentReference { get; set; }
    public bool PaymentVerified { get; set; }
    public decimal InspectionFee { get; set; }
    public string Status { get; set; } = "Pending";
}
