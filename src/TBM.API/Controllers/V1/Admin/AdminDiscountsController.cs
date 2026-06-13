using Microsoft.AspNetCore.Mvc;
using TBM.Application.DTOs.Subscriptions;
using TBM.Application.Services.Subscriptions;

namespace TBM.API.Controllers.V1.Admin;

public class AdminDiscountsController : BaseAdminController
{
    private readonly DiscountAdminService _discountService;

    public AdminDiscountsController(DiscountAdminService discountService)
    {
        _discountService = discountService;
    }

    /// <summary>GET /api/v1/admin/admindiscounts — list all active discounts.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var discounts = await _discountService.GetAllAsync();
        return Ok(new { success = true, data = discounts });
    }

    /// <summary>GET /api/v1/admin/admindiscounts/{id} — get a discount by ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var discount = await _discountService.GetByIdAsync(id);
        if (discount == null)
        {
            return NotFound(new { success = false, message = "Discount not found." });
        }

        return Ok(new { success = true, data = discount });
    }

    /// <summary>POST /api/v1/admin/admindiscounts — create a new discount/promo code.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDiscountDto dto)
    {
        try
        {
            var discount = await _discountService.CreateAsync(dto);
            return Ok(new { success = true, data = discount });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>PUT /api/v1/admin/admindiscounts/{id} — update an existing discount.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDiscountDto dto)
    {
        var updated = await _discountService.UpdateAsync(id, dto);
        if (updated == null)
        {
            return NotFound(new { success = false, message = "Discount not found." });
        }

        return Ok(new { success = true, data = updated });
    }

    /// <summary>DELETE /api/v1/admin/admindiscounts/{id} — deactivate a discount.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _discountService.DeleteAsync(id);
        if (!deleted)
        {
            return NotFound(new { success = false, message = "Discount not found." });
        }

        return Ok(new { success = true, message = "Discount deactivated." });
    }
}
