using TBM.Application.DTOs.Orders;
using TBM.Application.DTOs.Subscriptions;

namespace TBM.Application.DTOs.Auth;

public class TokenResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserInfoDto User { get; set; } = null!;
    public CartDto? Cart { get; set; }
    public bool CartMergeAttempted { get; set; }
    public bool CartMergeSucceeded { get; set; }
    public string? CartMergeMessage { get; set; }
    public List<MergeCartWarningDto> CartMergeWarnings { get; set; } = new();
}

public class UserInfoDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public CurrentSubscriptionDto? Subscription { get; set; }
}
