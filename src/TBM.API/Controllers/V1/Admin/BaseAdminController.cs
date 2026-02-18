using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace TBM.API.Controllers.V1.Admin;

[ApiController]
[Route("api/admin/[controller]")]
[Authorize(Roles = "SuperAdmin,Admin")]
[EnableRateLimiting("DynamicPolicy")]

public abstract class BaseAdminController : ControllerBase
{
    protected Guid GetCurrentUserId()
    {
        return Guid.Parse(User.FindFirst("nameidentifier")!.Value);
    }
}
