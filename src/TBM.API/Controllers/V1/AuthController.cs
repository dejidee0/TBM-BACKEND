using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TBM.Application.DTOs.Auth;
using TBM.Application.DTOs.Common;
using TBM.Application.Interfaces;

namespace TBM.API.Controllers.V1;

[ApiController]
[EnableRateLimiting("DynamicPolicy")]
[Route("api/v1/[controller]")]

public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IConfiguration _configuration;
    
    public AuthController(IAuthService authService, IConfiguration configuration)
    {
        _authService = authService;
        _configuration = configuration;
    }
    
    /// <summary>
    /// Register a new user account
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        var result = await _authService.RegisterAsync(dto);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return Ok(result);
    }
    
    /// <summary>
    /// Login with email and password
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var result = await _authService.LoginAsync(dto);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return Ok(result);
    }
    
    /// <summary>
    /// Refresh access token using refresh token
    /// </summary>
    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto dto)
    {
        var result = await _authService.RefreshTokenAsync(dto.RefreshToken);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return Ok(result);
    }
    
    /// <summary>
    /// Request password reset email
    /// </summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        var result = await _authService.ForgotPasswordAsync(dto);
        return Ok(result);
    }
    
    /// <summary>
    /// Reset password with token
    /// </summary>
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        var result = await _authService.ResetPasswordAsync(dto);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return Ok(result);
    }
    
    /// <summary>
    /// Verify email address with token query OR body payload { email, code }
    /// </summary>
    [HttpGet("verify-email")]
    public async Task<IActionResult> VerifyEmailByToken([FromQuery] string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(ApiResponse<bool>.ErrorResponse(
                "Provide token query parameter, e.g. /api/v1/auth/verify-email?token=..."
            ));
        }

        var result = await _authService.VerifyEmailAsync(token);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Verify email address with token query parameter
    /// </summary>
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromQuery] string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(ApiResponse<bool>.ErrorResponse(
                "Token query parameter is required. Usage: POST /api/v1/auth/verify-email?token=..."
            ));
        }
        
        var result = await _authService.VerifyEmailAsync(token);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return Ok(result);
    }
    
    /// <summary>
    /// Resend email verification link
    /// </summary>
    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification([FromBody] ForgotPasswordDto dto)
    {
        var result = await _authService.ResendVerificationEmailAsync(dto.Email);
        return Ok(result);
    }
    
    /// <summary>
    /// Get current authenticated user info
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    public IActionResult GetCurrentUser()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        var name = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
        var roles = User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();
        
        return Ok(new
        {
            userId,
            email,
            name,
            roles
        });
    }
    
    /// <summary>
    /// Logout (client should discard tokens)
    /// </summary>
    [Authorize]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        // In a stateless JWT system, logout is handled client-side by discarding tokens
        // Optionally, you could invalidate the refresh token in the database here
        return Ok(new { message = "Logged out successfully" });
    }

    /// <summary>
    /// Returns configured social auth provider capability flags.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("providers")]
    public IActionResult GetProviders()
    {
        var googleEnabled = _configuration.GetValue<bool>("AuthProviders:Google:Enabled");
        var appleEnabled = _configuration.GetValue<bool>("AuthProviders:Apple:Enabled");

        return Ok(new
        {
            google = new
            {
                enabled = googleEnabled,
                implemented = false
            },
            apple = new
            {
                enabled = appleEnabled,
                implemented = false
            }
        });
    }

    /// <summary>
    /// Google OAuth is currently not configured on this backend.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("~/auth/google")]
    public IActionResult GoogleOAuth()
    {
        var result = ApiResponse<bool>.ErrorResponse(
            "Google OAuth is not configured yet.",
            new List<string>
            {
                "Use POST /api/v1/auth/login with email/password.",
                "If OAuth is required, configure provider credentials and callback settings."
            });

        return StatusCode(StatusCodes.Status501NotImplemented, result);
    }

    /// <summary>
    /// Apple OAuth is currently not configured on this backend.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("~/auth/apple")]
    public IActionResult AppleOAuth()
    {
        var result = ApiResponse<bool>.ErrorResponse(
            "Apple OAuth is not configured yet.",
            new List<string>
            {
                "Use POST /api/v1/auth/login with email/password.",
                "If OAuth is required, configure provider credentials and callback settings."
            });

        return StatusCode(StatusCodes.Status501NotImplemented, result);
    }
}
