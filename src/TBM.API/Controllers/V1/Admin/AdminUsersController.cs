using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        int page = 1,
        int pageSize = 20,
        string? search = null,
        string? role = null,
        UserStatus? status = null)
    {
        var result = await _service.GetUsersAsync(page, pageSize, search, role, status);
        return Ok(result);
    }

    [HttpPatch("{id}/suspend")]
    public async Task<IActionResult> Suspend(Guid id)
    {
        var adminId = Guid.Parse(User.FindFirst("nameidentifier")!.Value);
        await _service.SuspendUserAsync(id, adminId);
        return Ok("User suspended");
    }

    [HttpPatch("{id}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id)
    {
        await _service.ReactivateUserAsync(id);
        return Ok("User reactivated");
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var adminId = Guid.Parse(User.FindFirst("nameidentifier")!.Value);
        await _service.SoftDeleteUserAsync(id, adminId);
        return Ok("User deleted");
    }
}
