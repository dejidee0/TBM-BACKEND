namespace TBM.Application.DTOs.Admin;

public class AdminUserDetailsDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Status { get; set; } = null!;
    public List<string> Roles { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}
