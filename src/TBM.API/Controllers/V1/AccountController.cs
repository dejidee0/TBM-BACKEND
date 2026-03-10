using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TBM.Application.Helpers;
using TBM.Application.Services;
using TBM.Core.Entities.Users;
using TBM.Core.Enums;
using TBM.Core.Interfaces;
using TBM.Core.Interfaces.Services;

namespace TBM.API.Controllers.V1;

[ApiController]
[Route("api/v1/account")]
[EnableRateLimiting("DynamicPolicy")]
[Authorize]
public class AccountController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserDataStoreService _store;
    private readonly ImageUploadService _imageUploadService;
    private readonly AuditService _auditService;
    private readonly IEmailService _emailService;

    public AccountController(
        IUnitOfWork unitOfWork,
        UserDataStoreService store,
        ImageUploadService imageUploadService,
        AuditService auditService,
        IEmailService emailService)
    {
        _unitOfWork = unitOfWork;
        _store = store;
        _imageUploadService = imageUploadService;
        _auditService = auditService;
        _emailService = emailService;
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
            return NotFound(new { success = false, message = "User not found" });

        var avatar = await GetAvatarAsync(user.Id);

        return Ok(new
        {
            id = user.Id,
            email = user.Email,
            firstName = user.FirstName,
            lastName = user.LastName,
            fullName = user.FullName,
            phoneNumber = user.PhoneNumber,
            isActive = user.IsActive,
            emailVerified = user.EmailVerified,
            avatarUrl = avatar,
            addresses = user.Addresses.Select(MapAddress)
        });
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
            return NotFound(new { success = false, message = "User not found" });

        var me = await BuildAccountMeResponseAsync(user);
        return Ok(new { success = true, data = me });
    }

    [HttpPatch("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateMeRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
            return NotFound(new { success = false, message = "User not found" });

        var changed = false;

        if (!string.IsNullOrWhiteSpace(request.FirstName))
        {
            user.FirstName = request.FirstName.Trim();
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(request.LastName))
        {
            user.LastName = request.LastName.Trim();
            changed = true;
        }

        if (request.PhoneNumber != null)
        {
            user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var normalized = request.Email.Trim().ToLowerInvariant();
            if (!string.Equals(user.Email, normalized, StringComparison.OrdinalIgnoreCase))
            {
                var exists = await _unitOfWork.Users.EmailExistsAsync(normalized);
                if (exists)
                    return BadRequest(new { success = false, message = "Email already in use" });

                user.Email = normalized;
                user.EmailVerified = false;
                changed = true;
            }
        }

        if (!changed)
            return BadRequest(new { success = false, message = "No valid changes supplied" });

        await _unitOfWork.SaveChangesAsync();
        await _auditService.LogAsync("Account.Me.Update", "Account", null, new
        {
            userId = user.Id,
            request.FirstName,
            request.LastName,
            request.PhoneNumber,
            request.Email
        });

        var me = await BuildAccountMeResponseAsync(user);
        return Ok(new { success = true, data = me });
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
            return NotFound(new { success = false, message = "User not found" });

        user.FirstName = string.IsNullOrWhiteSpace(request.FirstName) ? user.FirstName : request.FirstName.Trim();
        user.LastName = string.IsNullOrWhiteSpace(request.LastName) ? user.LastName : request.LastName.Trim();
        user.PhoneNumber = request.PhoneNumber?.Trim() ?? user.PhoneNumber;

        await _unitOfWork.SaveChangesAsync();

        return Ok(new { success = true, data = new { user.FirstName, user.LastName, user.PhoneNumber } });
    }

    [HttpPut("email")]
    public async Task<IActionResult> UpdateEmail([FromBody] UpdateEmailRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { success = false, message = "Email is required" });

        var user = await GetCurrentUserAsync();
        if (user == null)
            return NotFound(new { success = false, message = "User not found" });

        var normalized = request.Email.Trim().ToLowerInvariant();
        if (!string.Equals(user.Email, normalized, StringComparison.OrdinalIgnoreCase))
        {
            var exists = await _unitOfWork.Users.EmailExistsAsync(normalized);
            if (exists)
                return BadRequest(new { success = false, message = "Email already in use" });
        }

        var oldEmail = user.Email;
        user.Email = normalized;
        user.EmailVerified = false;

        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync("Account.Email.Update", "Account", new { oldEmail }, new { newEmail = normalized });
        return Ok(new { success = true, message = "Email updated successfully" });
    }

    [HttpPut("phone")]
    public async Task<IActionResult> UpdatePhone([FromBody] UpdatePhoneRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
            return NotFound(new { success = false, message = "User not found" });

        user.PhoneNumber = request.Phone?.Trim();
        await _unitOfWork.SaveChangesAsync();

        return Ok(new { success = true, message = "Phone updated successfully" });
    }

    [HttpPost("addresses")]
    public async Task<IActionResult> AddAddress([FromBody] AddressRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
            return NotFound(new { success = false, message = "User not found" });

        var existingAddresses = await _unitOfWork.UserAddresses.GetByUserIdAsync(user.Id);
        var setAsDefault = request.IsDefault || !existingAddresses.Any();

        if (setAsDefault)
        {
            foreach (var existing in existingAddresses.Where(a => a.IsDefault))
            {
                existing.IsDefault = false;
                await _unitOfWork.UserAddresses.UpdateAsync(existing);
            }
        }

        var address = new UserAddress
        {
            UserId = user.Id,
            FullName = request.FullName,
            Street = request.Street,
            City = request.City,
            State = request.State,
            PostalCode = request.PostalCode,
            Country = request.Country,
            Phone = request.Phone,
            DeliveryNotes = request.DeliveryNotes,
            IsDefault = setAsDefault
        };

        await _unitOfWork.UserAddresses.CreateAsync(address);
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync("Account.Address.Add", "Account", null, MapAddress(address));

        return Ok(new { success = true, data = MapAddress(address) });
    }

    [HttpPut("addresses/{addressId:guid}")]
    public async Task<IActionResult> UpdateAddress(Guid addressId, [FromBody] AddressRequest request)
    {
        var userId = GetUserId();
        var address = await _unitOfWork.UserAddresses.GetByIdAsync(addressId);
        if (address == null || address.UserId != userId)
            return NotFound(new { success = false, message = "Address not found" });

        address.FullName = request.FullName;
        address.Street = request.Street;
        address.City = request.City;
        address.State = request.State;
        address.PostalCode = request.PostalCode;
        address.Country = request.Country;
        address.Phone = request.Phone;
        address.DeliveryNotes = request.DeliveryNotes;

        if (request.IsDefault)
        {
            var userAddresses = await _unitOfWork.UserAddresses.GetByUserIdAsync(userId);
            foreach (var item in userAddresses)
            {
                item.IsDefault = item.Id == addressId;
                await _unitOfWork.UserAddresses.UpdateAsync(item);
            }
        }
        else
        {
            await _unitOfWork.UserAddresses.UpdateAsync(address);
        }

        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync("Account.Address.Update", "Account", null, MapAddress(address));
        return Ok(new { success = true });
    }

    [HttpDelete("addresses/{addressId:guid}")]
    public async Task<IActionResult> DeleteAddress(Guid addressId)
    {
        var userId = GetUserId();
        var address = await _unitOfWork.UserAddresses.GetByIdAsync(addressId);
        if (address == null || address.UserId != userId)
            return NotFound(new { success = false, message = "Address not found" });

        var wasDefault = address.IsDefault;
        await _unitOfWork.UserAddresses.DeleteAsync(address);
        await _unitOfWork.SaveChangesAsync();

        if (wasDefault)
        {
            var remaining = await _unitOfWork.UserAddresses.GetByUserIdAsync(userId);
            var first = remaining.FirstOrDefault();
            if (first != null)
            {
                first.IsDefault = true;
                await _unitOfWork.UserAddresses.UpdateAsync(first);
                await _unitOfWork.SaveChangesAsync();
            }
        }

        await _auditService.LogAsync("Account.Address.Delete", "Account", MapAddress(address), null);
        return Ok(new { success = true });
    }

    [HttpPut("addresses/{addressId:guid}/default")]
    public async Task<IActionResult> SetDefaultAddress(Guid addressId)
    {
        var userId = GetUserId();
        var userAddresses = await _unitOfWork.UserAddresses.GetByUserIdAsync(userId);
        if (!userAddresses.Any(a => a.Id == addressId))
            return NotFound(new { success = false, message = "Address not found" });

        foreach (var item in userAddresses)
        {
            item.IsDefault = item.Id == addressId;
            await _unitOfWork.UserAddresses.UpdateAsync(item);
        }

        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync("Account.Address.SetDefault", "Account", null, new { addressId });
        return Ok(new { success = true });
    }

    [HttpPut("password")]
    public async Task<IActionResult> UpdatePassword([FromBody] UpdatePasswordRequest request)
    {
        return await ChangePasswordCoreAsync(
            request.CurrentPassword,
            request.NewPassword,
            request.NewPassword);
    }

    [HttpPost("password/otp/request")]
    public async Task<IActionResult> RequestPasswordChangeOtp([FromBody] PasswordOtpRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
            return BadRequest(new { success = false, message = "Current password is required" });

        var user = await GetCurrentUserAsync();
        if (user == null)
            return NotFound(new { success = false, message = "User not found" });

        if (!PasswordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
            return BadRequest(new { success = false, message = "Current password is invalid" });

        var state = await GetPasswordOtpStateAsync(user.Id);
        var now = DateTime.UtcNow;

        if (state.NextAllowedRequestAt.HasValue && state.NextAllowedRequestAt.Value > now)
        {
            var retryAfter = (int)Math.Ceiling((state.NextAllowedRequestAt.Value - now).TotalSeconds);
            return BadRequest(new
            {
                success = false,
                message = "Please wait before requesting another OTP.",
                retryAfterSeconds = retryAfter
            });
        }

        var otpCode = GenerateOtpCode();
        state.OtpHash = HashOtp(user.Id, otpCode);
        state.OtpExpiresAt = now.AddMinutes(10);
        state.FailedAttempts = 0;
        state.VerifiedUntil = null;
        state.NextAllowedRequestAt = now.AddMinutes(1);
        state.LastRequestedAt = now;

        await SavePasswordOtpStateAsync(user.Id, state);

        try
        {
            await _emailService.SendEmailAsync(
                user.Email,
                "TBM Password Change OTP",
                BuildPasswordOtpEmailBody(otpCode));
        }
        catch
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Unable to send OTP email right now. Please try again."
            });
        }

        await _auditService.LogAsync("Account.Password.OTP.Request", "Account", null, new { userId = user.Id });

        return Ok(new
        {
            success = true,
            message = "OTP sent to your email.",
            expiresInSeconds = 600
        });
    }

    [HttpPost("password/otp/verify")]
    public async Task<IActionResult> VerifyPasswordChangeOtp([FromBody] VerifyPasswordOtpRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OtpCode))
            return BadRequest(new { success = false, message = "OTP code is required" });

        var user = await GetCurrentUserAsync();
        if (user == null)
            return NotFound(new { success = false, message = "User not found" });

        var state = await GetPasswordOtpStateAsync(user.Id);
        var now = DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(state.OtpHash) ||
            !state.OtpExpiresAt.HasValue ||
            state.OtpExpiresAt.Value <= now)
        {
            return BadRequest(new
            {
                success = false,
                message = "OTP has expired or was not requested."
            });
        }

        var incomingHash = HashOtp(user.Id, request.OtpCode.Trim());
        if (!string.Equals(incomingHash, state.OtpHash, StringComparison.Ordinal))
        {
            state.FailedAttempts += 1;

            if (state.FailedAttempts >= 3)
            {
                state.OtpHash = null;
                state.OtpExpiresAt = null;
                state.VerifiedUntil = null;
                await SavePasswordOtpStateAsync(user.Id, state);

                return BadRequest(new
                {
                    success = false,
                    message = "Too many invalid OTP attempts. Request a new OTP."
                });
            }

            await SavePasswordOtpStateAsync(user.Id, state);

            return BadRequest(new
            {
                success = false,
                message = "Invalid OTP code.",
                remainingAttempts = 3 - state.FailedAttempts
            });
        }

        state.OtpHash = null;
        state.OtpExpiresAt = null;
        state.FailedAttempts = 0;
        state.VerifiedUntil = now.AddMinutes(10);
        await SavePasswordOtpStateAsync(user.Id, state);

        await _auditService.LogAsync("Account.Password.OTP.Verify", "Account", null, new { userId = user.Id });

        return Ok(new
        {
            success = true,
            message = "OTP verified. You can now change your password.",
            validForSeconds = 600
        });
    }

    [HttpPost("password/change")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        return await ChangePasswordCoreAsync(
            request.CurrentPassword,
            request.NewPassword,
            request.ConfirmNewPassword);
    }

    [HttpGet("security")]
    public async Task<IActionResult> GetSecurity()
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
            return NotFound(new { success = false, message = "User not found" });

        var state = await GetSecurityStateAsync(user.Id);
        return Ok(new { twoFactorEnabled = state.TwoFactorEnabled });
    }

    [HttpPut("security/2fa")]
    public async Task<IActionResult> UpdateTwoFactor([FromBody] UpdateTwoFactorRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
            return NotFound(new { success = false, message = "User not found" });

        var state = await GetSecurityStateAsync(user.Id);
        state.TwoFactorEnabled = request.Enabled;
        await SaveSecurityStateAsync(user.Id, state);

        await _auditService.LogAsync("Account.Security.2FA.Update", "Account", null,
            new { userId = user.Id, request.Enabled });

        return Ok(new { success = true });
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications()
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
            return NotFound(new { success = false, message = "User not found" });

        var state = await GetNotificationStateAsync(user.Id);
        return Ok(state);
    }

    [HttpPut("notifications")]
    public async Task<IActionResult> UpdateNotifications([FromBody] NotificationPreferenceState state)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
            return NotFound(new { success = false, message = "User not found" });

        await SaveNotificationStateAsync(user.Id, state);
        await _auditService.LogAsync("Account.Notifications.Update", "Account", null, state);
        return Ok(new { success = true });
    }

    [HttpGet("brand-access")]
    public async Task<IActionResult> GetBrandAccess()
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
            return NotFound(new { success = false, message = "User not found" });

        var roles = user.UserRoles.Select(r => r.Role.Name).ToList();
        return Ok(new
        {
            roles,
            canUseStore = roles.Contains(UserRoles.Customer),
            canUseAdmin = roles.Contains(UserRoles.Admin) || roles.Contains(UserRoles.SuperAdmin)
        });
    }

    [HttpPost("deactivate")]
    public async Task<IActionResult> Deactivate([FromBody] OptionalPasswordRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
            return NotFound(new { success = false, message = "User not found" });

        if (!string.IsNullOrWhiteSpace(request.Password) &&
            !PasswordHasher.VerifyPassword(request.Password, user.PasswordHash))
            return BadRequest(new { success = false, message = "Password is invalid" });

        user.IsActive = false;
        user.Status = UserStatus.Suspended;
        user.SuspendedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync("Account.Deactivate", "Account", null, new { userId = user.Id });
        return Ok(new { success = true, message = "Account deactivated" });
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteAccount([FromBody] OptionalPasswordRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
            return NotFound(new { success = false, message = "User not found" });

        if (string.IsNullOrWhiteSpace(request.Password) ||
            !PasswordHasher.VerifyPassword(request.Password, user.PasswordHash))
            return BadRequest(new { success = false, message = "Password is required and must be valid" });

        user.IsActive = false;
        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync("Account.Delete", "Account", new { userId = user.Id }, null);
        return Ok(new { success = true, message = "Account deleted successfully" });
    }

    [HttpPost("avatar")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> UploadAvatar([FromForm] UploadAvatarRequest request)
    {
        if (request.File == null || request.File.Length == 0)
            return BadRequest(new { success = false, message = "Avatar file is required" });

        var user = await GetCurrentUserAsync();
        if (user == null)
            return NotFound(new { success = false, message = "User not found" });

        var fileName = $"avatar-{user.Id:N}-{DateTime.UtcNow:yyyyMMddHHmmss}{Path.GetExtension(request.File.FileName)}";
        await using var stream = request.File.OpenReadStream();
        var avatarUrl = await _imageUploadService.UploadRoomAsync(stream, fileName, user.Id.ToString("N"));

        await SaveAvatarAsync(user.Id, avatarUrl);
        await _auditService.LogAsync("Account.Avatar.Upload", "Account", null, new { userId = user.Id, avatarUrl });

        return Ok(new { success = true, avatarUrl });
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private async Task<User?> GetCurrentUserAsync()
    {
        var userId = GetUserId();
        return await _unitOfWork.Users.GetByIdAsync(userId);
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedAccessException("User ID not found in token");

        return userId;
    }

    private async Task<object> BuildAccountMeResponseAsync(User user)
    {
        var avatar = await GetAvatarAsync(user.Id);
        var securityState = await GetSecurityStateAsync(user.Id);
        var notificationState = await GetNotificationStateAsync(user.Id);
        var roles = user.UserRoles.Select(r => r.Role.Name).ToList();

        return new
        {
            profile = new
            {
                id = user.Id,
                email = user.Email,
                firstName = user.FirstName,
                lastName = user.LastName,
                fullName = user.FullName,
                phoneNumber = user.PhoneNumber,
                isActive = user.IsActive,
                emailVerified = user.EmailVerified,
                avatarUrl = avatar
            },
            addresses = user.Addresses.Select(MapAddress),
            notifications = notificationState,
            security = new { twoFactorEnabled = securityState.TwoFactorEnabled },
            roles,
            access = new
            {
                canUseStore = roles.Contains(UserRoles.Customer),
                canUseAdmin = roles.Contains(UserRoles.Admin) || roles.Contains(UserRoles.SuperAdmin)
            }
        };
    }

    private async Task<IActionResult> ChangePasswordCoreAsync(
        string currentPassword,
        string newPassword,
        string confirmNewPassword)
    {
        if (string.IsNullOrWhiteSpace(currentPassword) ||
            string.IsNullOrWhiteSpace(newPassword) ||
            string.IsNullOrWhiteSpace(confirmNewPassword))
        {
            return BadRequest(new { success = false, message = "Current password, new password and confirm password are required" });
        }

        if (!string.Equals(newPassword, confirmNewPassword, StringComparison.Ordinal))
            return BadRequest(new { success = false, message = "New password and confirmation do not match" });

        if (string.Equals(currentPassword, newPassword, StringComparison.Ordinal))
            return BadRequest(new { success = false, message = "New password must be different from current password" });

        var user = await GetCurrentUserAsync();
        if (user == null)
            return NotFound(new { success = false, message = "User not found" });

        if (!PasswordHasher.VerifyPassword(currentPassword, user.PasswordHash))
            return BadRequest(new { success = false, message = "Current password is invalid" });

        var otpState = await GetPasswordOtpStateAsync(user.Id);
        var now = DateTime.UtcNow;
        if (!otpState.VerifiedUntil.HasValue || otpState.VerifiedUntil.Value <= now)
        {
            return BadRequest(new
            {
                success = false,
                message = "Password OTP verification is required. Request and verify OTP from your email first."
            });
        }

        user.PasswordHash = PasswordHasher.HashPassword(newPassword);
        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;
        await _unitOfWork.SaveChangesAsync();

        otpState.VerifiedUntil = null;
        otpState.OtpHash = null;
        otpState.OtpExpiresAt = null;
        otpState.FailedAttempts = 0;
        await SavePasswordOtpStateAsync(user.Id, otpState);

        await _auditService.LogAsync("Account.Password.Update", "Account", null, new
        {
            userId = user.Id,
            sessionsInvalidated = true
        });

        try
        {
            await _emailService.SendPasswordResetConfirmationAsync(user.Email, user.FullName);
        }
        catch
        {
            // password already changed; skip failing request if confirmation email fails
        }

        return Ok(new
        {
            success = true,
            message = "Password updated successfully. Please sign in again on your devices."
        });
    }

    private static object MapAddress(UserAddress address) => new
    {
        id = address.Id,
        fullName = address.FullName,
        street = address.Street,
        city = address.City,
        state = address.State,
        postalCode = address.PostalCode,
        country = address.Country,
        phone = address.Phone,
        deliveryNotes = address.DeliveryNotes,
        isDefault = address.IsDefault
    };

    private async Task<SecurityPreferenceState> GetSecurityStateAsync(Guid userId) =>
        await _store.GetAsync("UserPreferences", UserDataStoreService.BuildUserKey("security", userId), new SecurityPreferenceState());

    private async Task SaveSecurityStateAsync(Guid userId, SecurityPreferenceState state) =>
        await _store.SaveAsync("UserPreferences", UserDataStoreService.BuildUserKey("security", userId), state, "Security preferences");

    private async Task<NotificationPreferenceState> GetNotificationStateAsync(Guid userId) =>
        await _store.GetAsync("UserPreferences", UserDataStoreService.BuildUserKey("notifications", userId), new NotificationPreferenceState());

    private async Task SaveNotificationStateAsync(Guid userId, NotificationPreferenceState state) =>
        await _store.SaveAsync("UserPreferences", UserDataStoreService.BuildUserKey("notifications", userId), state, "Notification preferences");

    private async Task<string?> GetAvatarAsync(Guid userId)
    {
        var state = await _store.GetAsync("UserPreferences", UserDataStoreService.BuildUserKey("avatar", userId), new AvatarState());
        return state.AvatarUrl;
    }

    private async Task SaveAvatarAsync(Guid userId, string avatarUrl) =>
        await _store.SaveAsync("UserPreferences", UserDataStoreService.BuildUserKey("avatar", userId), new AvatarState { AvatarUrl = avatarUrl }, "Avatar preferences");

    private async Task<PasswordOtpState> GetPasswordOtpStateAsync(Guid userId) =>
        await _store.GetAsync("UserPreferences", UserDataStoreService.BuildUserKey("password-otp", userId), new PasswordOtpState());

    private async Task SavePasswordOtpStateAsync(Guid userId, PasswordOtpState state) =>
        await _store.SaveAsync("UserPreferences", UserDataStoreService.BuildUserKey("password-otp", userId), state, "Password change OTP");

    private static string GenerateOtpCode()
    {
        return RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
    }

    private static string HashOtp(Guid userId, string otpCode)
    {
        var payload = $"{userId:N}:{otpCode.Trim()}";
        var bytes = Encoding.UTF8.GetBytes(payload);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static string BuildPasswordOtpEmailBody(string otpCode)
    {
        return $"""
                <div style="font-family:Arial,sans-serif;max-width:600px">
                  <h2>TBM Password Change Verification</h2>
                  <p>Use this OTP code to verify your identity and change your password:</p>
                  <p style="font-size:28px;font-weight:700;letter-spacing:6px">{otpCode}</p>
                  <p>This code expires in 10 minutes. If you did not request this, please secure your account immediately.</p>
                </div>
                """;
    }

    // ── Request / State DTOs ───────────────────────────────────────────────

    public class UpdateProfileRequest
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
    }

    public class UpdateMeRequest
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
    }

    public class UpdateEmailRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    public class UpdatePhoneRequest
    {
        public string? Phone { get; set; }
    }

    public class AddressRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? DeliveryNotes { get; set; }
        public bool IsDefault { get; set; }
    }

    public class UpdatePasswordRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class PasswordOtpRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;
    }

    public class VerifyPasswordOtpRequest
    {
        public string OtpCode { get; set; } = string.Empty;
    }

    public class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }

    public class UpdateTwoFactorRequest
    {
        public bool Enabled { get; set; }
    }

    public class OptionalPasswordRequest
    {
        public string? Password { get; set; }
    }

    public class UploadAvatarRequest
    {
        public IFormFile? File { get; set; }
    }

    public class SecurityPreferenceState
    {
        public bool TwoFactorEnabled { get; set; }
    }

    public class NotificationPreferenceState
    {
        public bool EmailOrderUpdates { get; set; } = true;
        public bool EmailPromotions { get; set; } = false;
        public bool PushOrderUpdates { get; set; } = true;
        public bool PushMarketing { get; set; } = false;
    }

    public class AvatarState
    {
        public string? AvatarUrl { get; set; }
    }

    public class PasswordOtpState
    {
        public string? OtpHash { get; set; }
        public DateTime? OtpExpiresAt { get; set; }
        public int FailedAttempts { get; set; }
        public DateTime? VerifiedUntil { get; set; }
        public DateTime? NextAllowedRequestAt { get; set; }
        public DateTime? LastRequestedAt { get; set; }
    }
}
