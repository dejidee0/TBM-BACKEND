using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TBM.Application.DTOs.Vendor;
using TBM.Application.Services;

namespace TBM.API.Controllers.V1.Admin;

[ApiController]
[Route("api/admin/vendors")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminVendorsController : ControllerBase
{
    private readonly VendorDomainService _vendorService;

    public AdminVendorsController(VendorDomainService vendorService)
    {
        _vendorService = vendorService;
    }

    [HttpPost("{userId:guid}/activate")]
    public async Task<IActionResult> ActivateVendor(Guid userId, [FromBody] ActivateVendorRequest request)
    {
        try
        {
            await _vendorService.ActivateVendorAsync(GetAdminId(), userId, request);
            return Ok(new { success = true });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("products/{productId:guid}/assign")]
    public async Task<IActionResult> AssignProductOwner(Guid productId, [FromBody] AssignProductOwnerRequest request)
    {
        try
        {
            var result = await _vendorService.AssignProductOwnershipAsync(GetAdminId(), productId, request.VendorUserId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("products/{productId:guid}/assign")]
    public async Task<IActionResult> RemoveProductOwner(Guid productId)
    {
        await _vendorService.RemoveProductOwnershipAsync(GetAdminId(), productId);
        return Ok(new { success = true });
    }

    [HttpGet("{vendorUserId:guid}/ownership")]
    public async Task<IActionResult> GetVendorOwnership(Guid vendorUserId)
    {
        var result = await _vendorService.GetVendorOwnershipAsync(vendorUserId);
        return Ok(result);
    }

    private Guid GetAdminId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(claim) || !Guid.TryParse(claim, out var id))
        {
            throw new UnauthorizedAccessException("Admin user ID not found in token.");
        }

        return id;
    }
}
