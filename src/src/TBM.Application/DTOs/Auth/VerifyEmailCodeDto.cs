using System.ComponentModel.DataAnnotations;

namespace TBM.Application.DTOs.Auth;

public class VerifyEmailCodeDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Code { get; set; } = string.Empty;
}
