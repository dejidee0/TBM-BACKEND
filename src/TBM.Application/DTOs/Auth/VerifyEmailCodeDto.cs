using System.ComponentModel.DataAnnotations;

namespace TBM.Application.DTOs.Auth;

public class VerifyEmailCodeDto
{
    [EmailAddress]
    public string? Email { get; set; }

    public string? Code { get; set; }
}
