using TBM.Application.DTOs.Orders;

namespace TBM.Application.DTOs.Auth;

public class LoginDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public List<MergeCartItemDto> GuestCartItems { get; set; } = new();
    public List<MergeCartItemDto> Items { get; set; } = new();
    public List<MergeCartItemDto> CartItems { get; set; } = new();
}
