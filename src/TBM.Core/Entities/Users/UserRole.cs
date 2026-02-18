namespace TBM.Core.Entities.Users;

public class UserRole
{
    public Guid UserId { get; set; }
    public virtual User User { get; set; } = null!;
    
    public Guid RoleId { get; set; }
    public virtual Role Role { get; set; } = null!;
    
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}