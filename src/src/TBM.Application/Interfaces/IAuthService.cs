using TBM.Application.DTOs.Auth;
using TBM.Application.DTOs.Common;

namespace TBM.Application.Interfaces;

public interface IAuthService
{
    Task<ApiResponse<TokenResponseDto>> RegisterAsync(RegisterDto dto);
    Task<ApiResponse<TokenResponseDto>> LoginAsync(LoginDto dto);
    Task<ApiResponse<TokenResponseDto>> AdminLoginAsync(AdminLoginDto dto);
    Task<ApiResponse<TokenResponseDto>> RefreshTokenAsync(string refreshToken);
    Task<ApiResponse<bool>> ForgotPasswordAsync(ForgotPasswordDto dto);
    Task<ApiResponse<bool>> ResetPasswordAsync(ResetPasswordDto dto);
    Task<ApiResponse<bool>> VerifyEmailAsync(string token);
    Task<ApiResponse<bool>> VerifyEmailWithCodeAsync(string email, string code);
    Task<ApiResponse<bool>> ResendVerificationEmailAsync(string email);
}
