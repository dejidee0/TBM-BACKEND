using TBM.Core.Entities.Common;
using TBM.Core.Enums;

namespace TBM.Core.Entities.Users;

public class User : AuditableEntity
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }

    public UserStatus Status { get; set; } = UserStatus.Active;


public DateTime? SuspendedAt { get; set; }
public Guid? SuspendedBy { get; set; }
    
    // Email Verification
    public bool EmailVerified { get; set; } = false;
    public string? EmailVerificationToken { get; set; }
    public DateTime? EmailVerificationTokenExpiry { get; set; }
    
    // Password Reset
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiry { get; set; }
    
    // Account Status
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public int FailedLoginAttempts { get; set; } = 0;
    public DateTime? LockoutEnd { get; set; }
    
    // Refresh Token
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }
    
    // Navigation Properties
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public virtual ICollection<UserAddress> Addresses { get; set; } = new List<UserAddress>();
    
    // Computed Property
    public string FullName => $"{FirstName} {LastName}".Trim();
}