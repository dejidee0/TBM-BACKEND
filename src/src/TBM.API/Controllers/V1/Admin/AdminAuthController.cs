using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TBM.Application.DTOs.Auth;
using TBM.Application.DTOs.Common;
using TBM.Application.Interfaces;
using TBM.Core.Interfaces;

[ApiController]
[Route("api/admin/auth")]
[EnableRateLimiting("DynamicPolicy")]

public class AdminAuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUnitOfWork _unitOfWork;

    public AdminAuthController(
        IAuthService authService,
        IUnitOfWork unitOfWork)
    {
        _authService = authService;
        _unitOfWork = unitOfWork;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(AdminLoginDto dto)
    {
        var result = await _authService.AdminLoginAsync(dto);
        return Ok(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(RefreshTokenDto dto)
    {
        var result = await _authService.RefreshTokenAsync(dto.RefreshToken);
        return Ok(result);
    }

    [HttpPost("logout")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Logout(RefreshTokenDto dto)
    {
        var user = await _unitOfWork.Users.GetByRefreshTokenAsync(dto.RefreshToken);

        if (user != null)
        {
            user.RefreshToken = null;
            user.RefreshTokenExpiry = null;

            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();
        }

        return Ok(ApiResponse<bool>.SuccessResponse(true, "Logged out successfully"));
    }
}
