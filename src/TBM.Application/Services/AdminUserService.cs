using System.Text;
using TBM.Application.Common;
using TBM.Application.DTOs.Admin;
using TBM.Application.Helpers;
using TBM.Core.Entities.Users;
using TBM.Core.Enums;
using TBM.Core.Interfaces;

namespace TBM.Application.Services;

public class AdminUserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly AuditService _audit;

    public AdminUserService(IUnitOfWork unitOfWork, AuditService audit)
    {
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    public async Task<PagedResult<AdminUserListDto>> GetUsersAsync(
        int page,
        int pageSize,
        string? search,
        string? role,
        UserStatus? status)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : pageSize;

        var query = _unitOfWork.Users.GetQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u =>
                u.Email.Contains(term) ||
                u.FirstName.Contains(term) ||
                u.LastName.Contains(term));
        }

        if (status.HasValue)
        {
            query = query.Where(u => u.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            var roleTerm = role.Trim();
            query = query.Where(u => u.UserRoles.Any(r => r.Role.Name == roleTerm));
        }

        var total = query.Count();

        var users = query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new AdminUserListDto
            {
                Id = u.Id,
                Email = u.Email,
                FullName = (u.FirstName + " " + u.LastName).Trim(),
                Status = u.Status.ToString(),
                Roles = u.UserRoles.Select(r => r.Role.Name).ToList(),
                CreatedAt = u.CreatedAt
            })
            .ToList();

        return new PagedResult<AdminUserListDto>
        {
            Items = users,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AdminUserDetailsDto> GetUserByIdAsync(Guid userId)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        return MapUserDetails(user);
    }

    public async Task<AdminUserDetailsDto> CreateUserAsync(AdminCreateUserRequestDto request, Guid adminId)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new InvalidOperationException("Email is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
        {
            throw new InvalidOperationException("Password must be at least 6 characters.");
        }

        if (await _unitOfWork.Users.EmailExistsAsync(request.Email))
        {
            throw new InvalidOperationException("Email already registered.");
        }

        var normalizedRole = NormalizeRoleOrThrow(
            string.IsNullOrWhiteSpace(request.Role) ? UserRoles.Customer : request.Role);
        var role = await GetOrCreateRoleAsync(normalizedRole);
        var (firstName, lastName) = ParseName(request.FullName, request.Email);

        var user = new User
        {
            Email = request.Email.Trim().ToLowerInvariant(),
            PasswordHash = PasswordHasher.HashPassword(request.Password),
            FirstName = firstName,
            LastName = lastName,
            IsActive = request.IsActive,
            Status = request.IsActive ? UserStatus.Active : UserStatus.Suspended,
            EmailVerified = false
        };

        user.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = role.Id
        });

        await _unitOfWork.Users.CreateAsync(user);
        await _unitOfWork.SaveChangesAsync();

        await _audit.LogAsync(
            "Admin.User.Create",
            "AdminUsers",
            null,
            new { adminId, userId = user.Id, user.Email, role = normalizedRole, request.IsActive });

        return MapUserDetails(user);
    }

    public async Task<AdminUserDetailsDto> UpdateUserStatusAsync(Guid userId, bool isActive, Guid adminId)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        var oldValue = new { user.IsActive, user.Status, user.SuspendedAt, user.SuspendedBy };

        user.IsActive = isActive;
        user.Status = isActive ? UserStatus.Active : UserStatus.Suspended;
        if (isActive)
        {
            user.SuspendedAt = null;
            user.SuspendedBy = null;
        }
        else
        {
            user.SuspendedAt = DateTime.UtcNow;
            user.SuspendedBy = adminId;
        }

        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();

        await _audit.LogAsync(
            "Admin.User.Status.Update",
            "AdminUsers",
            oldValue,
            new { userId, user.IsActive, user.Status, changedBy = adminId });

        return MapUserDetails(user);
    }

    public async Task<AdminUserDetailsDto> UpdateUserRoleAsync(Guid userId, string newRole, Guid adminId)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        var normalizedRole = NormalizeRoleOrThrow(newRole);
        var role = await GetOrCreateRoleAsync(normalizedRole);

        var oldRoles = user.UserRoles.Select(x => x.Role.Name).ToList();
        var currentRoles = user.UserRoles.ToList();
        foreach (var current in currentRoles)
        {
            user.UserRoles.Remove(current);
        }

        user.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = role.Id
        });

        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();

        await _audit.LogAsync(
            "Admin.User.Role.Update",
            "AdminUsers",
            new { userId, oldRoles },
            new { userId, roles = new[] { normalizedRole }, changedBy = adminId });

        return MapUserDetails(user);
    }

    public async Task<string> ExportUsersAsync(Guid adminId)
    {
        var rows = _unitOfWork.Users.GetQueryable()
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.FirstName,
                u.LastName,
                Status = u.Status.ToString(),
                u.IsActive,
                Roles = string.Join("|", u.UserRoles.Select(r => r.Role.Name)),
                u.CreatedAt
            })
            .ToList();

        var csv = new StringBuilder();
        csv.AppendLine("id,email,firstName,lastName,status,isActive,roles,createdAt");
        foreach (var row in rows)
        {
            csv.AppendLine(string.Join(',',
                row.Id,
                EscapeCsv(row.Email),
                EscapeCsv(row.FirstName),
                EscapeCsv(row.LastName),
                EscapeCsv(row.Status),
                row.IsActive,
                EscapeCsv(row.Roles),
                row.CreatedAt.ToString("O")));
        }

        var fileName = $"admin-users-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";

        await _audit.LogAsync(
            "AdminExport",
            "Users",
            null,
            new { adminId, fileName, rows = rows.Count });

        return fileName;
    }

    public async Task SuspendUserAsync(Guid userId, Guid adminId)
    {
        await UpdateUserStatusAsync(userId, false, adminId);
    }

    public async Task ReactivateUserAsync(Guid userId)
    {
        await UpdateUserStatusAsync(userId, true, Guid.Empty);
    }

    public async Task SoftDeleteUserAsync(Guid userId, Guid adminId)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        var oldValue = new { user.IsDeleted, user.DeletedAt, user.DeletedBy };

        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;
        user.DeletedBy = adminId.ToString();

        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();

        await _audit.LogAsync(
            "Admin.User.Delete",
            "AdminUsers",
            oldValue,
            new { userId, user.IsDeleted, changedBy = adminId });
    }

    private async Task<Role> GetOrCreateRoleAsync(string roleName)
    {
        var role = await _unitOfWork.Roles.GetByNameAsync(roleName);
        if (role != null)
        {
            return role;
        }

        role = new Role
        {
            Name = roleName,
            Description = $"{roleName} role"
        };

        await _unitOfWork.Roles.CreateAsync(role);
        await _unitOfWork.SaveChangesAsync();
        return role;
    }

    private static string NormalizeRoleOrThrow(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            throw new InvalidOperationException("Role is required.");
        }

        var normalized = role.Trim();
        if (normalized.Equals(UserRoles.Customer, StringComparison.OrdinalIgnoreCase)) return UserRoles.Customer;
        if (normalized.Equals(UserRoles.Vendor, StringComparison.OrdinalIgnoreCase)) return UserRoles.Vendor;
        if (normalized.Equals(UserRoles.Admin, StringComparison.OrdinalIgnoreCase)) return UserRoles.Admin;
        if (normalized.Equals(UserRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase)) return UserRoles.SuperAdmin;

        throw new InvalidOperationException("Invalid role.");
    }

    private static (string FirstName, string LastName) ParseName(string? fullName, string email)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            var fallback = email.Split('@')[0];
            return (fallback, "User");
        }

        var parts = fullName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 1)
        {
            return (parts[0], "User");
        }

        return (parts[0], string.Join(' ', parts.Skip(1)));
    }

    private static AdminUserDetailsDto MapUserDetails(User user)
    {
        return new AdminUserDetailsDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Status = user.Status.ToString(),
            Roles = user.UserRoles.Select(x => x.Role.Name).ToList(),
            CreatedAt = user.CreatedAt
        };
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
