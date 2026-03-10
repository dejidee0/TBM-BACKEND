namespace TBM.Application.DTOs.Admin;

public class AdminCreateUserRequestDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string Password { get; set; } = string.Empty;
}

public class AdminUpdateUserStatusRequestDto
{
    public bool IsActive { get; set; }
}

public class AdminUpdateUserRoleRequestDto
{
    public string NewRole { get; set; } = string.Empty;
}
