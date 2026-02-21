using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TBM.Application.Helpers;
using TBM.Application.Services;
using TBM.Core.Entities.Users;
using TBM.Core.Enums;
using TBM.Core.Interfaces;

namespace TBM.API.Controllers.V1;

[ApiController]
[Route("api/v1/[controller]")]
[EnableRateLimiting("DynamicPolicy")]
[Authorize]
public class AccountController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserDataStoreService _store;
    private readonly ImageUploadService _imageUploadService;
    private readonly AuditService _auditService;

    public AccountController(
        IUnitOfWork unitOfWork,
        UserDataStoreService store,
        ImageUploadService imageUploadService,
        AuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _store = store;
        _imageUploadService = imageUploadService;
        _auditService = auditService;
    }

    [HttpGet("~/api/account/profile")]
    public async Task<IActionResult> GetProfile()
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return NotFound(new { success = false, message = "User not found" });
        }

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

    [HttpPut("~/api/account/profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return NotFound(new { success = false, message = "User not found" });
        }

        user.FirstName = string.IsNullOrWhiteSpace(request.FirstName) ? user.FirstName : request.FirstName.Trim();
        user.LastName = string.IsNullOrWhiteSpace(request.LastName) ? user.LastName : request.LastName.Trim();
        user.PhoneNumber = request.PhoneNumber?.Trim() ?? user.PhoneNumber;

        await _unitOfWork.SaveChangesAsync();

        return Ok(new { success = true, data = new { user.FirstName, user.LastName, user.PhoneNumber } });
    }

    [HttpPut("~/api/account/email")]
    public async Task<IActionResult> UpdateEmail([FromBody] UpdateEmailRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { success = false, message = "Email is required" });
        }

        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return NotFound(new { success = false, message = "User not found" });
        }

        var normalized = request.Email.Trim().ToLowerInvariant();
        if (!string.Equals(user.Email, normalized, StringComparison.OrdinalIgnoreCase))
        {
            var exists = await _unitOfWork.Users.EmailExistsAsync(normalized);
            if (exists)
            {
                return BadRequest(new { success = false, message = "Email already in use" });
            }
        }

        var oldEmail = user.Email;
        user.Email = normalized;
        user.EmailVerified = false;

        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync("Account.Email.Update", "Account", new { oldEmail }, new { newEmail = normalized });
        return Ok(new { success = true, message = "Email updated successfully" });
    }

    [HttpPut("~/api/account/phone")]
    public async Task<IActionResult> UpdatePhone([FromBody] UpdatePhoneRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return NotFound(new { success = false, message = "User not found" });
        }

        user.PhoneNumber = request.Phone?.Trim();
        await _unitOfWork.SaveChangesAsync();

        return Ok(new { success = true, message = "Phone updated successfully" });
    }

    [HttpPost("~/api/account/addresses")]
    public async Task<IActionResult> AddAddress([FromBody] AddressRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return NotFound(new { success = false, message = "User not found" });
        }

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

    [HttpPut("~/api/account/addresses/{addressId:guid}")]
    public async Task<IActionResult> UpdateAddress(Guid addressId, [FromBody] AddressRequest request)
    {
        var userId = GetUserId();
        var address = await _unitOfWork.UserAddresses.GetByIdAsync(addressId);
        if (address == null || address.UserId != userId)
        {
            return NotFound(new { success = false, message = "Address not found" });
        }

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

    [HttpDelete("~/api/account/addresses/{addressId:guid}")]
    public async Task<IActionResult> DeleteAddress(Guid addressId)
    {
        var userId = GetUserId();
        var address = await _unitOfWork.UserAddresses.GetByIdAsync(addressId);
        if (address == null || address.UserId != userId)
        {
            return NotFound(new { success = false, message = "Address not found" });
        }

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

    [HttpPut("~/api/account/addresses/{addressId:guid}/default")]
    public async Task<IActionResult> SetDefaultAddress(Guid addressId)
    {
        var userId = GetUserId();
        var userAddresses = await _unitOfWork.UserAddresses.GetByUserIdAsync(userId);
        if (!userAddresses.Any(a => a.Id == addressId))
        {
            return NotFound(new { success = false, message = "Address not found" });
        }

        foreach (var item in userAddresses)
        {
            item.IsDefault = item.Id == addressId;
            await _unitOfWork.UserAddresses.UpdateAsync(item);
        }

        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync("Account.Address.SetDefault", "Account", null, new { addressId });
        return Ok(new { success = true });
    }

    [HttpPut("~/api/account/password")]
    public async Task<IActionResult> UpdatePassword([FromBody] UpdatePasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new { success = false, message = "Current and new password are required" });
        }

        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return NotFound(new { success = false, message = "User not found" });
        }

        var isCurrentValid = PasswordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash);
        if (!isCurrentValid)
        {
            return BadRequest(new { success = false, message = "Current password is invalid" });
        }

        user.PasswordHash = PasswordHasher.HashPassword(request.NewPassword);
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync("Account.Password.Update", "Account", null, new { userId = user.Id });
        return Ok(new { success = true, message = "Password updated successfully" });
    }

    [HttpGet("~/api/account/security")]
    public async Task<IActionResult> GetSecurity()
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return NotFound(new { success = false, message = "User not found" });
        }

        var state = await GetSecurityStateAsync(user.Id);
        return Ok(new
        {
            twoFactorEnabled = state.TwoFactorEnabled
        });
    }

    [HttpPut("~/api/account/security/2fa")]
    public async Task<IActionResult> UpdateTwoFactor([FromBody] UpdateTwoFactorRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return NotFound(new { success = false, message = "User not found" });
        }

        var state = await GetSecurityStateAsync(user.Id);
        state.TwoFactorEnabled = request.Enabled;
        await SaveSecurityStateAsync(user.Id, state);

        await _auditService.LogAsync(
            "Account.Security.2FA.Update",
            "Account",
            null,
            new { userId = user.Id, request.Enabled });

        return Ok(new { success = true });
    }

    [HttpGet("~/api/account/notifications")]
    public async Task<IActionResult> GetNotifications()
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return NotFound(new { success = false, message = "User not found" });
        }

        var state = await GetNotificationStateAsync(user.Id);
        return Ok(state);
    }

    [HttpPut("~/api/account/notifications")]
    public async Task<IActionResult> UpdateNotifications([FromBody] NotificationPreferenceState state)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return NotFound(new { success = false, message = "User not found" });
        }

        await SaveNotificationStateAsync(user.Id, state);
        await _auditService.LogAsync("Account.Notifications.Update", "Account", null, state);
        return Ok(new { success = true });
    }

    [HttpGet("~/api/account/brand-access")]
    public async Task<IActionResult> GetBrandAccess()
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return NotFound(new { success = false, message = "User not found" });
        }

        var roles = user.UserRoles.Select(r => r.Role.Name).ToList();
        return Ok(new
        {
            roles,
            canUseStore = roles.Contains(UserRoles.Customer),
            canUseAdmin = roles.Contains(UserRoles.Admin) || roles.Contains(UserRoles.SuperAdmin)
        });
    }

    [HttpPost("~/api/account/deactivate")]
    public async Task<IActionResult> Deactivate([FromBody] OptionalPasswordRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return NotFound(new { success = false, message = "User not found" });
        }

        if (!string.IsNullOrWhiteSpace(request.Password) &&
            !PasswordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return BadRequest(new { success = false, message = "Password is invalid" });
        }

        user.IsActive = false;
        user.Status = UserStatus.Suspended;
        user.SuspendedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync("Account.Deactivate", "Account", null, new { userId = user.Id });
        return Ok(new { success = true, message = "Account deactivated" });
    }

    [HttpDelete("~/api/account")]
    public async Task<IActionResult> DeleteAccount([FromBody] OptionalPasswordRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return NotFound(new { success = false, message = "User not found" });
        }

        if (string.IsNullOrWhiteSpace(request.Password) ||
            !PasswordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return BadRequest(new { success = false, message = "Password is required and must be valid" });
        }

        user.IsActive = false;
        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync("Account.Delete", "Account", new { userId = user.Id }, null);
        return Ok(new { success = true, message = "Account deleted successfully" });
    }

    [HttpPost("~/api/account/avatar")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> UploadAvatar([FromForm] UploadAvatarRequest request)
    {
        if (request.File == null || request.File.Length == 0)
        {
            return BadRequest(new { success = false, message = "Avatar file is required" });
        }

        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return NotFound(new { success = false, message = "User not found" });
        }

        var fileName = $"avatar-{user.Id:N}-{DateTime.UtcNow:yyyyMMddHHmmss}{Path.GetExtension(request.File.FileName)}";
        await using var stream = request.File.OpenReadStream();
        var avatarUrl = await _imageUploadService.UploadRoomAsync(stream, fileName, user.Id.ToString("N"));

        await SaveAvatarAsync(user.Id, avatarUrl);
        await _auditService.LogAsync("Account.Avatar.Upload", "Account", null, new { userId = user.Id, avatarUrl });

        return Ok(new { success = true, avatarUrl });
    }

    private async Task<User?> GetCurrentUserAsync()
    {
        var userId = GetUserId();
        return await _unitOfWork.Users.GetByIdAsync(userId);
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("User ID not found in token");
        }

        return userId;
    }

    private static object MapAddress(UserAddress address)
    {
        return new
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
    }

    private async Task<SecurityPreferenceState> GetSecurityStateAsync(Guid userId)
    {
        return await _store.GetAsync("UserPreferences", UserDataStoreService.BuildUserKey("security", userId), new SecurityPreferenceState());
    }

    private async Task SaveSecurityStateAsync(Guid userId, SecurityPreferenceState state)
    {
        await _store.SaveAsync(
            "UserPreferences",
            UserDataStoreService.BuildUserKey("security", userId),
            state,
            "Security preferences");
    }

    private async Task<NotificationPreferenceState> GetNotificationStateAsync(Guid userId)
    {
        return await _store.GetAsync(
            "UserPreferences",
            UserDataStoreService.BuildUserKey("notifications", userId),
            new NotificationPreferenceState());
    }

    private async Task SaveNotificationStateAsync(Guid userId, NotificationPreferenceState state)
    {
        await _store.SaveAsync(
            "UserPreferences",
            UserDataStoreService.BuildUserKey("notifications", userId),
            state,
            "Notification preferences");
    }

    private async Task<string?> GetAvatarAsync(Guid userId)
    {
        var state = await _store.GetAsync("UserPreferences", UserDataStoreService.BuildUserKey("avatar", userId), new AvatarState());
        return state.AvatarUrl;
    }

    private async Task SaveAvatarAsync(Guid userId, string avatarUrl)
    {
        await _store.SaveAsync(
            "UserPreferences",
            UserDataStoreService.BuildUserKey("avatar", userId),
            new AvatarState { AvatarUrl = avatarUrl },
            "Avatar preferences");
    }

    public class UpdateProfileRequest
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
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
}
