namespace TBM.Application.DTOs.Auth;

public class AdminLoginDto
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public bool RememberMe { get; set; }
}
