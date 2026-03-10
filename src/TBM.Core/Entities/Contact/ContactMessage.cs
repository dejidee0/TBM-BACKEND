using TBM.Core.Entities.Common;

namespace TBM.Core.Entities.Contact;

public class ContactMessage : AuditableEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Subject { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool EmailSent { get; set; }
    public string? EmailError { get; set; }
}
