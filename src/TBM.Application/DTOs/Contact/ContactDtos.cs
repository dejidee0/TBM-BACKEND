using System.ComponentModel.DataAnnotations;

namespace TBM.Application.DTOs.Contact;

public class CreateContactMessageDto
{
    [Required]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? PhoneNumber { get; set; }

    [MaxLength(300)]
    public string? Subject { get; set; }

    [Required]
    [MaxLength(5000)]
    public string Message { get; set; } = string.Empty;
}

public class ContactSubmissionResultDto
{
    public bool Accepted { get; set; }
    public Guid ReferenceId { get; set; }
}
