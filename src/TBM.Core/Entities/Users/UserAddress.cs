using TBM.Core.Entities.Common;

namespace TBM.Core.Entities.Users;

public class UserAddress : BaseEntity
{
    public Guid UserId { get; set; }
    public virtual User User { get; set; } = null!;
    
    public string FullName { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? DeliveryNotes { get; set; }
    public bool IsDefault { get; set; } = false;
}