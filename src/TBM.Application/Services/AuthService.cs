using TBM.Application.DTOs.Auth;
using TBM.Application.DTOs.Common;
using TBM.Application.Helpers;
using TBM.Application.Interfaces; 
using TBM.Core.Entities.Users;
using TBM.Core.Enums;
using TBM.Core.Interfaces;
using TBM.Core.Interfaces.Services;


namespace TBM.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly JwtHelper _jwtHelper;
    private readonly IEmailService _emailService;

    public AuthService(IUnitOfWork unitOfWork, JwtHelper jwtHelper,IEmailService emailService)
    {
        _unitOfWork = unitOfWork;
        _jwtHelper = jwtHelper;
        _emailService = emailService;
    }
    
    public async Task<ApiResponse<TokenResponseDto>> RegisterAsync(RegisterDto dto)
{
    // Validate passwords match
    if (dto.Password != dto.ConfirmPassword)
    {
        return ApiResponse<TokenResponseDto>.ErrorResponse("Passwords do not match");
    }
    
    // Check if email already exists
    if (await _unitOfWork.Users.EmailExistsAsync(dto.Email))
    {
        return ApiResponse<TokenResponseDto>.ErrorResponse("Email already registered");
    }
    
    // Create new user
    var user = new User
    {
        Email = dto.Email.ToLower(),
        PasswordHash = PasswordHasher.HashPassword(dto.Password),
        FirstName = dto.FirstName,
        LastName = dto.LastName,
        PhoneNumber = dto.PhoneNumber,
        EmailVerified = false,
        EmailVerificationToken = GenerateVerificationToken(),
        EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24),
        IsActive = true
    };
    
    try
    {
        // Create user
        await _unitOfWork.Users.CreateAsync(user);
        
        // Assign Customer role by default
        var customerRole = await _unitOfWork.Roles.GetByNameAsync(UserRoles.Customer);
        if (customerRole == null)
        {
            // Create Customer role if it doesn't exist
            customerRole = new Role
            {
                Name = UserRoles.Customer,
                Description = "Regular customer with access to shop and purchase"
            };
            await _unitOfWork.Roles.CreateAsync(customerRole);
            await _unitOfWork.SaveChangesAsync();
        }
        
        var userRole = new UserRole
        {
            UserId = user.Id,
            RoleId = customerRole.Id
        };
        
        user.UserRoles.Add(userRole);
        await _unitOfWork.SaveChangesAsync();
        
        // TODO: Send verification email (Phase 8)
        var verificationLink = $"https://tdm-web.vercel.app/verify-email?token={user.EmailVerificationToken}";

await _emailService.SendVerificationEmailAsync(
    user.Email,
    user.FullName,
    verificationLink
);

        
        // Generate tokens
        var roles = new List<string> { UserRoles.Customer };
        var accessToken = _jwtHelper.GenerateAccessToken(user, roles);
        var refreshToken = _jwtHelper.GenerateRefreshToken();
        
        // Save refresh token
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();
        
        var response = new TokenResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            User = new UserInfoDto
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FullName = user.FullName,
                Roles = roles
            }
        };
        
        return ApiResponse<TokenResponseDto>.SuccessResponse(
            response, 
            "Registration successful. Please verify your email."
        );
    }
    catch (Exception ex)
    {
        return ApiResponse<TokenResponseDto>.ErrorResponse($"Registration failed: {ex.Message}");
    }
}
    
    public async Task<ApiResponse<TokenResponseDto>> LoginAsync(LoginDto dto)
    {
        // Get user with roles
        var user = await _unitOfWork.Users.GetByEmailWithRolesAsync(dto.Email);
        
        if (user == null)
        {
            return ApiResponse<TokenResponseDto>.ErrorResponse("Invalid email or password");
        }
        
        // Check if account is locked
        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow)
        {
            var remainingTime = (user.LockoutEnd.Value - DateTime.UtcNow).Minutes;
            return ApiResponse<TokenResponseDto>.ErrorResponse(
                $"Account is locked. Try again in {remainingTime} minutes."
            );
        }
        
        // Verify password
        if (!PasswordHasher.VerifyPassword(dto.Password, user.PasswordHash))
        {
            // Increment failed login attempts
            user.FailedLoginAttempts++;
            
            // Lock account after 5 failed attempts
            if (user.FailedLoginAttempts >= 5)
            {
                user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
                user.FailedLoginAttempts = 0;
                await _unitOfWork.Users.UpdateAsync(user);
                await _unitOfWork.SaveChangesAsync();
                
                return ApiResponse<TokenResponseDto>.ErrorResponse(
                    "Account locked due to multiple failed login attempts. Try again in 15 minutes."
                );
            }
            
            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();
            
            return ApiResponse<TokenResponseDto>.ErrorResponse("Invalid email or password");
        }
        
        // Check if account is active
        if (!user.IsActive)
        {
            return ApiResponse<TokenResponseDto>.ErrorResponse("Account is inactive");
        }
        
        // Reset failed login attempts and update last login
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        user.LastLoginAt = DateTime.UtcNow;
        
        // Get user roles
        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        
        // Generate tokens
        var accessToken = _jwtHelper.GenerateAccessToken(user, roles);
        var refreshToken = _jwtHelper.GenerateRefreshToken();
        
        // Save refresh token
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        
        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();
        
        var response = new TokenResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            User = new UserInfoDto
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FullName = user.FullName,
                Roles = roles
            }
        };
        
        return ApiResponse<TokenResponseDto>.SuccessResponse(response, "Login successful");
    }
    
    public async Task<ApiResponse<TokenResponseDto>> RefreshTokenAsync(string refreshToken)
    {
        var user = await _unitOfWork.Users.GetByRefreshTokenAsync(refreshToken);
        
        if (user == null)
        {
            return ApiResponse<TokenResponseDto>.ErrorResponse("Invalid or expired refresh token");
        }
        
        // Get user roles
        var roles = await _unitOfWork.Users.GetUserRolesAsync(user.Id);
        
        // Generate new tokens
        var accessToken = _jwtHelper.GenerateAccessToken(user, roles);
        var newRefreshToken = _jwtHelper.GenerateRefreshToken();
        
        // Update refresh token
        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        
        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();
        
        var response = new TokenResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            User = new UserInfoDto
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FullName = user.FullName,
                Roles = roles.ToList()
            }
        };
        
        return ApiResponse<TokenResponseDto>.SuccessResponse(response, "Token refreshed successfully");
    }
    
    public async Task<ApiResponse<bool>> ForgotPasswordAsync(ForgotPasswordDto dto)
    {
        var user = await _unitOfWork.Users.GetByEmailAsync(dto.Email);
        
        if (user == null)
        {
            // Don't reveal if email exists
            return ApiResponse<bool>.SuccessResponse(true, 
                "If the email exists, a password reset link has been sent.");
        }
        
        // Generate reset token
        user.PasswordResetToken = GenerateVerificationToken();
        user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);
        
        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();
        
        // TODO: Send password reset email (Phase 8)
        // await _emailService.SendPasswordResetEmailAsync(user.Email, user.PasswordResetToken);
        
        return ApiResponse<bool>.SuccessResponse(true, 
            "If the email exists, a password reset link has been sent.");
    }
    
    public async Task<ApiResponse<bool>> ResetPasswordAsync(ResetPasswordDto dto)
    {
        if (dto.NewPassword != dto.ConfirmPassword)
        {
            return ApiResponse<bool>.ErrorResponse("Passwords do not match");
        }
        
        var user = await _unitOfWork.Users.GetByPasswordResetTokenAsync(dto.Token);
        
        if (user == null)
        {
            return ApiResponse<bool>.ErrorResponse("Invalid or expired reset token");
        }
        
        // Update password
        user.PasswordHash = PasswordHasher.HashPassword(dto.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiry = null;
        
        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();
        
        return ApiResponse<bool>.SuccessResponse(true, "Password reset successful");
    }
    
    public async Task<ApiResponse<bool>> VerifyEmailAsync(string token)
    {
        var user = await _unitOfWork.Users.GetByEmailVerificationTokenAsync(token);
        
        if (user == null)
        {
            return ApiResponse<bool>.ErrorResponse("Invalid or expired verification token");
        }
        
        user.EmailVerified = true;
        user.EmailVerificationToken = null;
        user.EmailVerificationTokenExpiry = null;
        
        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();
        
        return ApiResponse<bool>.SuccessResponse(true, "Email verified successfully");
    }

   public async Task<ApiResponse<TokenResponseDto>> AdminLoginAsync(AdminLoginDto dto)
{
    var user = await _unitOfWork.Users.GetByEmailWithRolesAsync(dto.Email);

    if (user == null)
        return ApiResponse<TokenResponseDto>.ErrorResponse("Invalid email or password");

    if (!PasswordHasher.VerifyPassword(dto.Password, user.PasswordHash))
        return ApiResponse<TokenResponseDto>.ErrorResponse("Invalid email or password");

    if (!user.IsActive)
        return ApiResponse<TokenResponseDto>.ErrorResponse("Account is inactive");

    var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();

    // ✅ CRITICAL: Only allow Admin / SuperAdmin
    if (!roles.Contains(UserRoles.Admin) && !roles.Contains(UserRoles.SuperAdmin))
        return ApiResponse<TokenResponseDto>.ErrorResponse("Access denied");

    user.LastLoginAt = DateTime.UtcNow;
    user.FailedLoginAttempts = 0;

    var accessToken = _jwtHelper.GenerateAccessToken(user, roles);
    var refreshToken = _jwtHelper.GenerateRefreshToken();

    user.RefreshToken = refreshToken;
    user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);

    await _unitOfWork.Users.UpdateAsync(user);
    await _unitOfWork.SaveChangesAsync();

    var response = new TokenResponseDto
    {
        AccessToken = accessToken,
        RefreshToken = refreshToken,
        ExpiresAt = DateTime.UtcNow.AddMinutes(15),
        User = new UserInfoDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = user.FullName,
            Roles = roles
        }
    };

    return ApiResponse<TokenResponseDto>.SuccessResponse(response, "Admin login successful");
}


    
    public async Task<ApiResponse<bool>> ResendVerificationEmailAsync(string email)
    {
        var user = await _unitOfWork.Users.GetByEmailAsync(email);
        
        if (user == null || user.EmailVerified)
        {
            return ApiResponse<bool>.SuccessResponse(true, 
                "If the email exists and is unverified, a verification link has been sent.");
        }
        
        // Generate new verification token
        user.EmailVerificationToken = GenerateVerificationToken();
        user.EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24);
        
        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();
        
        // TODO: Send verification email (Phase 8)
       var verificationLink = $"https://tdm-web.vercel.app/verify-email?token={user.EmailVerificationToken}";

await _emailService.SendVerificationEmailAsync(
    user.Email,
    user.FullName,
    verificationLink
);

        
        return ApiResponse<bool>.SuccessResponse(true, 
            "If the email exists and is unverified, a verification link has been sent.");
    }

    
    
    private static string GenerateVerificationToken()
    {
        return Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
    }
}