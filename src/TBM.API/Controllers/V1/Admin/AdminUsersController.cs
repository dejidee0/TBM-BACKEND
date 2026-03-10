using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TBM.Application.DTOs.Admin;
using TBM.Application.Services;
using TBM.Core.Enums;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminUsersController : ControllerBase
{
    private readonly AdminUserService _service;

    public AdminUsersController(AdminUserService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery(Name = "limit")] int limit = 20,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? search = null,
        [FromQuery] string? role = null,
        [FromQuery] UserStatus? status = null)
    {
        var effectivePageSize = pageSize ?? limit;
        var result = await _service.GetUsersAsync(page, effectivePageSize, search, role, status);
        var totalPages = result.TotalCount == 0
            ? 0
            : (int)Math.Ceiling(result.TotalCount / (double)result.PageSize);

        return Ok(new
        {
            users = result.Items,
            pagination = new
            {
                page = result.Page,
                limit = result.PageSize,
                total = result.TotalCount,
                totalPages
            },
            items = result.Items,
            totalCount = result.TotalCount,
            page = result.Page,
            pageSize = result.PageSize
        });
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportUsers()
    {
        var adminId = GetCurrentUserId();
        var fileName = await _service.ExportUsersAsync(adminId);
        return Ok(new { success = true, filename = fileName });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetUser(Guid id)
    {
        try
        {
            var user = await _service.GetUserByIdAsync(id);
            return Ok(user);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] AdminCreateUserRequestDto request)
    {
        try
        {
            var adminId = GetCurrentUserId();
            var user = await _service.CreateUserAsync(request, adminId);
            return Ok(user);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] AdminUpdateUserStatusRequestDto request)
    {
        try
        {
            var adminId = GetCurrentUserId();
            var user = await _service.UpdateUserStatusAsync(id, request.IsActive, adminId);
            return Ok(new { success = true, user });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPatch("{id:guid}/role")]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] AdminUpdateUserRoleRequestDto request)
    {
        try
        {
            var adminId = GetCurrentUserId();
            var user = await _service.UpdateUserRoleAsync(id, request.NewRole, adminId);
            return Ok(new { success = true, user });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPatch("{id}/suspend")]
    public async Task<IActionResult> Suspend(Guid id)
    {
        try
        {
            var adminId = GetCurrentUserId();
            await _service.UpdateUserStatusAsync(id, false, adminId);
            return Ok(new { success = true });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPatch("{id}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id)
    {
        try
        {
            var adminId = GetCurrentUserId();
            await _service.UpdateUserStatusAsync(id, true, adminId);
            return Ok(new { success = true });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var adminId = GetCurrentUserId();
            await _service.SoftDeleteUserAsync(id, adminId);
            return Ok(new { success = true });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("nameidentifier")?.Value;

        if (string.IsNullOrWhiteSpace(claim) || !Guid.TryParse(claim, out var userId))
        {
            throw new UnauthorizedAccessException("User ID claim not found.");
        }

        return userId;
    }
}
