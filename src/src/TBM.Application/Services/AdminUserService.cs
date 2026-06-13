
using TBM.Application.Common;
using TBM.Application.DTOs.Admin;
using TBM.Core.Enums;
using TBM.Core.Interfaces;

namespace TBM.Application.Services;

public class AdminUserService
{
    private readonly IUnitOfWork _unitOfWork;

    public AdminUserService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<AdminUserListDto>> GetUsersAsync(
        int page,
        int pageSize,
        string? search,
        string? role,
        UserStatus? status)
    {
        var query = _unitOfWork.Users.GetQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u =>
                u.Email.Contains(search) ||
                u.FirstName.Contains(search) ||
                u.LastName.Contains(search));
        }

        if (status.HasValue)
        {
            query = query.Where(u => u.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(u =>
                u.UserRoles.Any(r => r.Role.Name == role));
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
                FullName = u.FirstName + " " + u.LastName,
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

    public async Task SuspendUserAsync(Guid userId, Guid adminId)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null) throw new Exception("User not found");

        user.Status = UserStatus.Suspended;
        user.SuspendedAt = DateTime.UtcNow;
        user.SuspendedBy = adminId;

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task ReactivateUserAsync(Guid userId)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null) throw new Exception("User not found");

        user.Status = UserStatus.Active;
        user.SuspendedAt = null;
        user.SuspendedBy = null;

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task SoftDeleteUserAsync(Guid userId, Guid adminId)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null) throw new Exception("User not found");

        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;
        user.DeletedBy = adminId.ToString();

        await _unitOfWork.SaveChangesAsync();
    }
}
